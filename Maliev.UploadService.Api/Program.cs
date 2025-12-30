using Maliev.UploadService.Api.BackgroundServices;
using Maliev.UploadService.Api.Metrics;
using Maliev.UploadService.Api.Services;
using Maliev.UploadService.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

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
});

// Add services
builder.Services.AddScoped<IAuthorizationPolicyService, AuthorizationPolicyService>();

// IAM Services
builder.AddServiceClient<Maliev.Aspire.ServiceDefaults.IAM.IIamServiceClient, Maliev.UploadService.Api.Services.Auth.IamServiceClient>("IAM");
builder.Services.AddScoped<Maliev.UploadService.Api.Services.Auth.UploadIAMRegistrationService>();

// T082: Register FileValidationService and GcsStorageService
builder.Services.AddScoped<IValidationService, FileValidationService>();

// T137: Register LifecycleManagementService
builder.Services.AddScoped<ILifecycleManagementService, LifecycleManagementService>();

// T175: Register BulkDeleteService
builder.Services.AddScoped<IBulkDeleteService, BulkDeleteService>();

// T135: Register LifecyclePolicyWorker background service
builder.Services.AddHostedService<LifecyclePolicyWorker>();
builder.Services.AddSingleton(sp =>
{
    var bucketName = builder.Configuration["GoogleCloud:BucketName"] ?? "maliev-uploads";
    return Google.Cloud.Storage.V1.StorageClient.Create();
});
builder.Services.AddScoped<IStorageService>(sp =>
{
    var storageClient = sp.GetRequiredService<Google.Cloud.Storage.V1.StorageClient>();
    var bucketName = builder.Configuration["GoogleCloud:BucketName"] ?? "maliev-uploads";
    return new GcsStorageService(storageClient, bucketName);
});
builder.Services.AddSingleton<nClam.IClamClient>(sp =>
{
    var clamHost = builder.Configuration["ClamAV:Host"] ?? "localhost";
    var clamPort = int.Parse(builder.Configuration["ClamAV:Port"] ?? "3310");
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
app.UseHttpsRedirection();
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

/// <summary>
/// Main program class for the application
/// </summary>
public partial class Program { }

