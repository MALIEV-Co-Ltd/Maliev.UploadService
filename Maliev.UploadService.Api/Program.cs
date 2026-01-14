#pragma warning disable CA1848 // For improved performance, use the LoggerMessage delegates
using Maliev.Aspire.ServiceDefaults;
using Maliev.UploadService.Api.BackgroundServices;
using Maliev.UploadService.Api.Metrics;
using Maliev.UploadService.Api.Services;
using Maliev.UploadService.Api.Services.Auth;
using Maliev.UploadService.Data;
using Maliev.MessagingContracts.Generated;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scalar.AspNetCore;

// Initialize bootstrap logging
using var loggerFactory = LoggerFactory.Create(logBuilder => logBuilder.AddConsole());
var bootstrapLogger = loggerFactory.CreateLogger("Program");

try
{
    bootstrapLogger.LogInformation("Starting Upload Service host");

    var builder = WebApplication.CreateBuilder(args);

    // --- Secrets & Configuration ---
    builder.AddGoogleSecretManagerVolume(); // Load secrets from /mnt/secrets if available

    // --- Infrastructure & Observability ---
    builder.AddServiceDefaults(); // OpenTelemetry, health checks, resilience
    builder.AddStandardMiddleware(options =>
    {
        options.EnableRequestLogging = true;
    });
    builder.AddServiceMeters("uploads-meter"); // Register service meters for OpenTelemetry business metrics

    // JWT Authentication (tests override via PostConfigureAll with dynamic RSA keys)
    builder.AddJwtAuthentication();

    builder.Services.AddAuthorization();

    // --- API Configuration ---
    builder.AddDefaultCors(); // CORS from CORS:AllowedOrigins config
    builder.AddDefaultApiVersioning(); // API versioning with URL segment reader

    // Add OpenAPI (must be in Program.cs for XML comments to work via source generator)
    if (!builder.Environment.IsProduction())
    {
        builder.AddStandardOpenApi(
            title: "MALIEV Upload Service API",
            description: "Centralized file upload and storage service for the Maliev platform. Provides secure file upload with validation, lifecycle management, signed URLs for access control, and async event notifications.");
    }

    // T150: Configure FormOptions for large file handling (FR-023)
    builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
    {
        options.MultipartBodyLengthLimit = 10L * 1024 * 1024 * 1024; // 10GB
        options.ValueLengthLimit = int.MaxValue;
        options.MultipartHeadersLengthLimit = int.MaxValue;
    });

    // T151: Configure Kestrel for 10GB max request body size (FR-023)
    builder.WebHost.ConfigureKestrel(serverOptions =>
    {
        serverOptions.Limits.MaxRequestBodySize = 10L * 1024 * 1024 * 1024; // 10GB
        serverOptions.Limits.MinRequestBodyDataRate = null; // No minimum data rate for large uploads
        serverOptions.Limits.MinResponseDataRate = null;
        serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(10); // Longer timeout for large uploads
        serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
    });

    // Add controllers with JSON options configured for camelCase
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        });

    // --- Infrastructure (Use ServiceDefaults extensions) ---
    // Database: Connects + sets up Retry Policy + Health Check
    builder.AddPostgresDbContext<UploadDbContext>("UploadDbContext", true, (Action<IServiceProvider, DbContextOptionsBuilder>?)null);

    // Cache: Connects + sets up Health Check
    builder.AddRedisDistributedCache(instanceName: "upload:");

    // Messaging (RabbitMQ)
    // Note: Service Defaults handles host configuration from "rabbitmq" connection string
    builder.AddMassTransitWithRabbitMq(configurator =>
    {
        // T174: Register BulkDeleteJobConsumer
        configurator.AddConsumer<Maliev.UploadService.Api.Consumers.BulkDeleteJobConsumer>();

        // Configure message topology for routing keys: maliev.uploadservice.v1.{entity}.{action}
        configurator.SetKebabCaseEndpointNameFormatter();
    }, (context, cfg) =>
    {
        // Configure central events exchange for interoperability
        cfg.Message<FileUploadedEvent>(m => m.SetEntityName("maliev.events"));
        cfg.Publish<FileUploadedEvent>(p =>
        {
            p.ExchangeType = "topic";
        });

        cfg.Message<FileDeletedEvent>(m => m.SetEntityName("maliev.events"));
        cfg.Publish<FileDeletedEvent>(p =>
        {
            p.ExchangeType = "topic";
        });

        // Configure routing keys for published events
        // In MassTransit, topic routing keys for publishing are configured via cfg.Send
        // when using topic exchange or directly in the Publish topology.
        cfg.Send<FileUploadedEvent>(s =>
        {
            s.UseRoutingKeyFormatter(ctx => "maliev.uploadservice.v1.upload.completed");
        });

        cfg.Send<FileDeletedEvent>(s =>
        {
            s.UseRoutingKeyFormatter(ctx => "maliev.uploadservice.v1.file.deleted");
        });

        cfg.ConfigureEndpoints(context);
    });

    // Add services
    builder.Services.AddScoped<IAuthorizationPolicyService, AuthorizationPolicyService>();

    // IAM Services
    builder.AddIAMServiceClient("upload");
    builder.Services.AddIAMRegistration<UploadIAMRegistrationService>("upload");

    // T082: Register FileValidationService and GcsStorageService
    builder.Services.AddScoped<IValidationService, FileValidationService>();

    // T137: Register LifecycleManagementService
    builder.Services.AddScoped<ILifecycleManagementService, LifecycleManagementService>();

    // T175: Register BulkDeleteService
    builder.Services.AddScoped<IBulkDeleteService, BulkDeleteService>();

    // T135: Register LifecyclePolicyWorker background service
    builder.Services.AddHostedService<LifecyclePolicyWorker>();

    var googleCloudEnabled = builder.Configuration.GetValue<bool>("GoogleCloud:Enabled", true);

    if (googleCloudEnabled)
    {
        builder.Services.AddSingleton(sp =>
        {
            return Google.Cloud.Storage.V1.StorageClient.Create();
        });
        builder.Services.AddScoped<IStorageService>(sp =>
        {
            var storageClient = sp.GetRequiredService<Google.Cloud.Storage.V1.StorageClient>();
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var bucketName = builder.Configuration["GoogleCloud:BucketName"] ?? "maliev-uploads";
            return new GcsStorageService(storageClient, bucketName, httpClientFactory);
        });
    }
    else
    {
        builder.Services.AddScoped<IStorageService, MockStorageService>();
    }

    builder.Services.AddSingleton<nClam.IClamClient>(sp =>
    {
        var clamHost = builder.Configuration["ClamAV:Host"] ?? "localhost";
        var clamPort = int.Parse(builder.Configuration["ClamAV:Port"] ?? "3310");
        var enabled = builder.Configuration.GetValue<bool>("ClamAV:Enabled", true);

        if (!enabled)
        {
            return new DummyClamClient(sp.GetRequiredService<ILogger<DummyClamClient>>());
        }

        return new nClam.ClamClient(clamHost, clamPort);
    });

    // T186: Register UploadMetrics for OpenTelemetry instrumentation (Constitution Principle XII)
    builder.Services.AddSingleton<UploadMetrics>();

    var app = builder.Build();
    var logger = app.Services.GetRequiredService<ILogger<Program>>();

    // --- Database Migrations ---
    await app.MigrateDatabaseAsync<UploadDbContext>();

    // --- Middleware Pipeline ---
    app.UseStandardMiddleware();
    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }
    app.UseRouting();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();

    // --- Endpoints ---
    app.MapControllers();
    app.MapDefaultEndpoints(servicePrefix: "upload"); // Health checks: /upload/liveness, /upload/readiness
    app.MapApiDocumentation(servicePrefix: "upload"); // OpenAPI: /upload/openapi/v1.json, Scalar UI: /upload/scalar

    logger.LogInformation("UploadService started successfully on {Environment} environment", app.Environment.EnvironmentName);

    await app.RunAsync();
}
catch (Exception ex)
{
    bootstrapLogger.LogCritical(ex, "Upload Service host terminated unexpectedly during startup");
    throw;
}
finally
{
    loggerFactory.Dispose();
}

/// <summary>
/// Main program class for the application
/// </summary>
public partial class Program { }
