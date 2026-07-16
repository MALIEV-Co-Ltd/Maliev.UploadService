using Maliev.MessagingContracts.Contracts.Uploads;
using Maliev.MessagingContracts;
using Maliev.UploadService.Api.BackgroundServices;
using Maliev.UploadService.Api.Metrics;
using Maliev.UploadService.Api.Services;
using Maliev.UploadService.Api.Services.Auth;
using Maliev.UploadService.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

// Initialize bootstrap logging
using var loggerFactory = LoggerFactory.Create(logBuilder => logBuilder.AddConsole());
var bootstrapLogger = loggerFactory.CreateLogger("Program");

try
{
    Program.Log.StartingHost(bootstrapLogger, "Upload Service");

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

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy(UploadAuthorizationPolicies.AuthenticatedSubject, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context =>
            {
                var subject = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                    ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                return !string.IsNullOrWhiteSpace(subject);
            });
        });
    });

    // --- API Configuration ---
    builder.AddStandardCors(); // CORS with fail-fast validation
    builder.Services.AddCors(options =>
    {
        options.AddPolicy(Program.MockStorageCorsPolicy, policy =>
        {
            policy.AllowAnyOrigin()
                .WithMethods(HttpMethods.Get, HttpMethods.Head, HttpMethods.Options)
                .AllowAnyHeader()
                .WithExposedHeaders("Accept-Ranges", "Content-Range", "Content-Length", "Content-Type");
        });
    });
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
    builder.AddStandardCache("upload:"); // Redis + in-memory fallback, memory-optimized

    // Messaging (RabbitMQ)
    // Note: Service Defaults handles host configuration from "rabbitmq" connection string
    builder.AddMassTransitWithRabbitMq(configurator =>
    {
        configurator.AddEntityFrameworkOutbox<UploadDbContext>(options =>
        {
            _ = options.UsePostgres();
            options.UseBusOutbox();
        });

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
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<UploadCallerContext>();

    // IAM Services
    builder.AddIAMServiceClient("upload");
    builder.Services.AddIAMRegistration<UploadIAMRegistrationService>("upload");

    // T082: Register FileValidationService and GcsStorageService
    builder.Services.AddScoped<IValidationService, FileValidationService>();

    // T137: Register LifecycleManagementService
    builder.Services.AddScoped<ILifecycleManagementService, LifecycleManagementService>();

    // T175: Register BulkDeleteService
    builder.Services.AddScoped<Maliev.UploadService.Application.Interfaces.IBulkDeleteService, BulkDeleteService>();

    // T135: Register LifecyclePolicyWorker background service
    builder.Services.AddHostedService<LifecyclePolicyWorker>();

    var googleCloudEnabled = builder.Configuration.GetValue<bool>("GoogleCloud:Enabled", true);

    if (googleCloudEnabled)
    {
        // Create GoogleCredential: use service account key from environment (local dev)
        // or fall back to Application Default Credentials (GKE Workload Identity in production)
        builder.Services.AddSingleton<Google.Apis.Auth.OAuth2.GoogleCredential>(sp =>
        {
            var keyBase64 = builder.Configuration["GCP:ServiceAccountKeyBase64"];
            if (!string.IsNullOrEmpty(keyBase64))
            {
                var keyJson = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(keyBase64));
                var credential = Google.Apis.Auth.OAuth2.CredentialFactory
                    .FromJson<Google.Apis.Auth.OAuth2.ServiceAccountCredential>(keyJson)
                    .ToGoogleCredential();
                return credential.IsCreateScopedRequired
                    ? credential.CreateScoped(Google.Apis.Storage.v1.StorageService.Scope.DevstorageFullControl)
                    : credential;
            }

            // Production: uses GKE Workload Identity via Application Default Credentials
            var applicationDefaultCredential = Google.Apis.Auth.OAuth2.GoogleCredential.GetApplicationDefault();
            return applicationDefaultCredential.IsCreateScopedRequired
                ? applicationDefaultCredential.CreateScoped(Google.Apis.Storage.v1.StorageService.Scope.DevstorageFullControl)
                : applicationDefaultCredential;
        });

        builder.Services.AddSingleton(sp =>
        {
            var credential = sp.GetRequiredService<Google.Apis.Auth.OAuth2.GoogleCredential>();
            return Google.Cloud.Storage.V1.StorageClient.Create(credential);
        });

        builder.Services.AddScoped<IStorageService>(sp =>
        {
            var storageClient = sp.GetRequiredService<Google.Cloud.Storage.V1.StorageClient>();
            var credential = sp.GetRequiredService<Google.Apis.Auth.OAuth2.GoogleCredential>();
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var config = sp.GetRequiredService<IConfiguration>();
            return new GcsStorageService(storageClient, config, httpClientFactory, credential);
        });

        // Register the Infrastructure IStorageService (Application.Interfaces namespace) for
        // components (e.g. AdminController) that depend on the extended interface with CopyFileAsync.
        builder.Services.AddScoped<Maliev.UploadService.Application.Interfaces.IStorageService>(sp =>
        {
            var storageClient = sp.GetRequiredService<Google.Cloud.Storage.V1.StorageClient>();
            var credential = sp.GetRequiredService<Google.Apis.Auth.OAuth2.GoogleCredential>();
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var config = sp.GetRequiredService<IConfiguration>();
            var logger = sp.GetRequiredService<ILogger<Maliev.UploadService.Infrastructure.Storage.GcsStorageService>>();
            return new Maliev.UploadService.Infrastructure.Storage.GcsStorageService(
                storageClient, config, httpClientFactory, credential, logger);
        });
    }
    else
    {
        builder.Services.AddScoped<IStorageService, MockStorageService>();
        builder.Services.AddScoped<Maliev.UploadService.Application.Interfaces.IStorageService,
            Maliev.UploadService.Infrastructure.Storage.MockStorageService>();
    }

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

    Program.Log.ServiceStarted(logger, "Upload Service");
    await app.RunAsync();
}
catch (Exception ex)
{
    Program.Log.HostTerminated(bootstrapLogger, ex, "Upload Service");
    throw;
}
finally
{
    loggerFactory.Dispose();
}

/// <summary>
/// Main program class for the application
/// </summary>
public partial class Program
{
    /// <summary>
    /// CORS policy used by local mock signed URLs so browser viewers can fetch artifacts directly.
    /// </summary>
    public const string MockStorageCorsPolicy = "MockStorageDownloads";

    internal static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Starting {ServiceName} host")]
        public static partial void StartingHost(ILogger logger, string serviceName);

        [LoggerMessage(Level = LogLevel.Critical, Message = "{ServiceName} host terminated unexpectedly during startup")]
        public static partial void HostTerminated(ILogger logger, Exception ex, string serviceName);

        [LoggerMessage(Level = LogLevel.Information, Message = "{ServiceName} started successfully")]
        public static partial void ServiceStarted(ILogger logger, string serviceName);
    }
}
