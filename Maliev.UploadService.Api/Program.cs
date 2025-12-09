using Asp.Versioning;
using Maliev.UploadService.Api.BackgroundServices;
using Maliev.UploadService.Api.Data;
using Maliev.UploadService.Api.Metrics;
using Maliev.UploadService.Api.Middleware;
using Maliev.UploadService.Api.Services;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// CONSTITUTION COMPLIANCE NOTE (Principle XIII - .NET Aspire Integration)
// ============================================================================
// The following ServiceDefaults calls are REQUIRED by constitution v1.7.0
// but are commented out pending GitHub Packages authentication setup:
//
// builder.AddGoogleSecretManagerVolume(); // Load secrets from /mnt/secrets
// builder.AddServiceDefaults(); // OpenTelemetry, health checks, resilience
// builder.AddServiceMeters("uploadservice"); // Business metrics registration
//
// Once Maliev.Aspire.ServiceDefaults package is available via GitHub Packages,
// uncomment these lines and remove manual infrastructure configuration below.
// ============================================================================

// Configure JWT Authentication (T048)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Authentication:Authority"];
        options.Audience = builder.Configuration["Authentication:Audience"];
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });

builder.Services.AddAuthorization();

// Add API Versioning (T057)
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'V";
    options.SubstituteApiVersionInUrl = true;
});

// Add OpenAPI/Scalar (T058)
builder.Services.AddOpenApi();

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

// Add controllers with JSON options configured to use PascalCase
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null; // Use PascalCase (default C# naming)
    });

// Add DbContext (with health check - T059)
builder.Services.AddDbContext<UploadServiceDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("UploadServiceDbContext")));

// Add Redis caching (with health check - T059)
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("redis");
});

// Add Health Checks (T059)
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("UploadServiceDbContext")!, name: "postgresql")
    .AddRedis(builder.Configuration.GetConnectionString("redis")!, name: "redis");

// T161: Configure MassTransit with RabbitMQ (FR-025)
builder.Services.AddMassTransit(x =>
{
    // T174: Register BulkDeleteJobConsumer
    x.AddConsumer<Maliev.UploadService.Api.Consumers.BulkDeleteJobConsumer>();

    // Configure message topology for routing keys: maliev.uploadservice.v1.{entity}.{action}
    x.SetKebabCaseEndpointNameFormatter();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("rabbitmq") ?? "localhost", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });

        // Configure exchange and routing keys
        cfg.Message<Maliev.UploadService.Api.Events.UploadCompletedEvent>(e =>
        {
            e.SetEntityName("maliev.uploadservice.v1.upload.completed");
        });

        cfg.Message<Maliev.UploadService.Api.Events.UploadFailedEvent>(e =>
        {
            e.SetEntityName("maliev.uploadservice.v1.upload.failed");
        });

        cfg.Message<Maliev.UploadService.Api.Events.FileDeletedEvent>(e =>
        {
            e.SetEntityName("maliev.uploadservice.v1.file.deleted");
        });

        cfg.ConfigureEndpoints(context);
    });
});

// Add services
builder.Services.AddScoped<IAuthorizationPolicyService, AuthorizationPolicyService>();

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

// Apply database migrations
await MigrateDatabaseAsync(app.Services);

// TODO: Map default endpoints (T060) - uncomment once ServiceDefaults is configured
// app.MapDefaultEndpoints(servicePrefix: "uploadservice");
// app.MapApiDocumentation(servicePrefix: "uploadservice");

// Configure middleware pipeline (Constitution best practice: CorrelationId before Exception handling)
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // Map Scalar API documentation (T060)
    app.MapScalarApiReference(options =>
    {
        options.Title = "Upload Service API";
        options.Theme = ScalarTheme.Purple;
    });
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

static async Task MigrateDatabaseAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<UploadServiceDbContext>();
    await context.Database.MigrateAsync();
}

// Make Program class accessible to tests
public partial class Program { }
