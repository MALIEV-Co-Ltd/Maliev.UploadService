using Maliev.Aspire.ServiceDefaults;
using Maliev.UploadService.Api.BackgroundServices;
using Maliev.UploadService.Api.Metrics;
using Maliev.UploadService.Api.Services;
using Maliev.UploadService.Api.Services.Auth;
using Maliev.UploadService.Data;
using Maliev.MessagingContracts.Generated;
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
}, (context, cfg) =>
{
    // Configure central events exchange for interoperability
    cfg.Message<FileUploadedEvent>(m => m.SetEntityName("maliev.events"));
    cfg.Publish<FileUploadedEvent>(p => p.ExchangeType = "topic");

    cfg.Message<FileDeletedEvent>(m => m.SetEntityName("maliev.events"));
    cfg.Publish<FileDeletedEvent>(p => p.ExchangeType = "topic");

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
        return new DummyClamClient();
    }

    return new nClam.ClamClient(clamHost, clamPort);
});

// T186: Register UploadMetrics for OpenTelemetry instrumentation (Constitution Principle XII)
builder.Services.AddSingleton<UploadMetrics>();

var app = builder.Build();
var logger = app.Services.GetRequiredService<ILogger<Program>>();

// --- Database Migrations ---
await app.MigrateDatabaseAsync<UploadDbContext>();

// Seed default policy for geometry-service if it doesn't exist (Development/Testing only)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<UploadDbContext>();
    if (!await dbContext.ServiceAuthorizationPolicies.AnyAsync(p => p.ServiceId == "geometry-service"))
    {
        dbContext.ServiceAuthorizationPolicies.Add(new Maliev.UploadService.Data.Entities.ServiceAuthorizationPolicy
        {
            PolicyId = "policy-geometry-service",
            ServiceId = "geometry-service",
            ServiceName = "Geometry Analysis Service",
            AllowedPathPrefixes = new List<string> { "geometry-test", "geometry/" },
            AllowedContentTypes = new List<string> { "application/octet-stream", "model/stl", "text/plain" },
            MaxFileSizeBytes = 100 * 1024 * 1024, // 100MB
            StorageQuotaBytes = 1024 * 1024 * 1024, // 1GB
            AllowOverwrite = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await dbContext.SaveChangesAsync();
    }
}

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

/// <summary>
/// Main program class for the application
/// </summary>
public partial class Program { }

#nullable disable
/// <summary>
/// Dummy ClamAV client that always returns a clean scan result.
/// Used when ClamAV scanning is disabled in configuration.
/// </summary>
public class DummyClamClient : nClam.IClamClient
{
    /// <inheritdoc />
    public int Port { get; set; } = 3310;

    /// <inheritdoc />
    public string Server { get; set; } = "localhost";

    /// <inheritdoc />
    public System.Net.IPAddress ServerIP { get; set; } = null;

    /// <inheritdoc />
    public int MaxChunkSize { get; set; } = 128 * 1024;

    /// <inheritdoc />
    public long MaxStreamSize { get; set; } = 0;

    /// <inheritdoc />
    public Task<nClam.ClamScanResult> SendAndScanFileAsync(Stream fileStream, CancellationToken cancellationToken = default) => Task.FromResult(new nClam.ClamScanResult("Mock Success"));

    /// <inheritdoc />
    public Task<nClam.ClamScanResult> SendAndScanFileAsync(Stream fileStream) => Task.FromResult(new nClam.ClamScanResult("Mock Success"));

    /// <inheritdoc />
    public Task<nClam.ClamScanResult> SendAndScanFileAsync(byte[] fileData, CancellationToken cancellationToken = default) => Task.FromResult(new nClam.ClamScanResult("Mock Success"));

    /// <inheritdoc />
    public Task<nClam.ClamScanResult> SendAndScanFileAsync(byte[] fileData) => Task.FromResult(new nClam.ClamScanResult("Mock Success"));

    /// <inheritdoc />
    public Task<nClam.ClamScanResult> SendAndScanFileAsync(string filePath, CancellationToken cancellationToken = default) => Task.FromResult(new nClam.ClamScanResult("Mock Success"));

    /// <inheritdoc />
    public Task<nClam.ClamScanResult> SendAndScanFileAsync(string filePath) => Task.FromResult(new nClam.ClamScanResult("Mock Success"));

    /// <inheritdoc />
    public Task<bool> PingAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    /// <inheritdoc />
    public Task<bool> PingAsync() => Task.FromResult(true);

    /// <inheritdoc />
    public Task<bool> TryPingAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    /// <inheritdoc />
    public Task<bool> TryPingAsync() => Task.FromResult(true);

    /// <inheritdoc />
    public Task<string> GetVersionAsync(CancellationToken cancellationToken = default) => Task.FromResult("Mock 1.0");

    /// <inheritdoc />
    public Task<string> GetVersionAsync() => Task.FromResult("Mock 1.0");

    /// <inheritdoc />
    public Task<nClam.ClamScanResult> ScanFileOnServerAsync(string filePath, CancellationToken cancellationToken = default) => Task.FromResult(new nClam.ClamScanResult("Mock Success"));

    /// <inheritdoc />
    public Task<nClam.ClamScanResult> ScanFileOnServerAsync(string filePath) => Task.FromResult(new nClam.ClamScanResult("Mock Success"));

    /// <inheritdoc />
    public Task<nClam.ClamScanResult> ScanFileOnServerMultithreadedAsync(string filePath, CancellationToken cancellationToken = default) => Task.FromResult(new nClam.ClamScanResult("Mock Success"));

    /// <inheritdoc />
    public Task<nClam.ClamScanResult> ScanFileOnServerMultithreadedAsync(string filePath) => Task.FromResult(new nClam.ClamScanResult("Mock Success"));

    /// <inheritdoc />
    public Task Shutdown(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task Shutdown() => Task.CompletedTask;
}

#nullable enable
/// <summary>
/// Mock storage service for testing environments.
/// </summary>
public class MockStorageService : IStorageService
{
    private readonly ILogger<MockStorageService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockStorageService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public MockStorageService(ILogger<MockStorageService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<StorageUploadResult> UploadFileAsync(
        Stream fileStream,
        string storagePath,
        string contentType,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MOCK: Uploading file to {StoragePath}", storagePath);
        return Task.FromResult(new StorageUploadResult
        {
            StoragePath = storagePath,
            ContentType = contentType,
            SizeBytes = fileStream.Length,
            UploadedAt = DateTime.UtcNow
        });
    }

    /// <inheritdoc />
    public Task<bool> FileExistsAsync(string storagePath, CancellationToken cancellationToken = default) => Task.FromResult(false);

    /// <inheritdoc />
    public Task DeleteFileAsync(string storagePath, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task<string> GenerateSignedUrlAsync(
        string storagePath,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"https://mock-storage.local/{storagePath}?token=mock-token");
    }

    /// <inheritdoc />
    public Task<StorageFileMetadata?> GetFileMetadataAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<StorageFileMetadata?>(new StorageFileMetadata
        {
            Name = storagePath,
            ContentType = "application/octet-stream",
            SizeBytes = 100,
            CreatedAt = DateTime.UtcNow,
            ETag = "mock-etag"
        });
    }

    /// <inheritdoc />
    public Task<ResumableUploadSession> InitiateResumableUploadAsync(
        string storagePath,
        string contentType,
        long totalSize,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ResumableUploadSession
        {
            SessionUri = $"https://mock-storage.local/upload/{Guid.NewGuid()}",
            StoragePath = storagePath,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        });
    }

    /// <inheritdoc />
    public Task<ResumableUploadProgress> ResumeUploadAsync(
        string sessionUri,
        Stream chunkStream,
        long startByte,
        long endByte,
        long totalSize,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ResumableUploadProgress
        {
            BytesReceived = endByte + 1,
            TotalSize = totalSize,
            IsComplete = endByte + 1 >= totalSize,
            StoragePath = endByte + 1 >= totalSize ? "mock-path" : null
        });
    }
}
#nullable restore
