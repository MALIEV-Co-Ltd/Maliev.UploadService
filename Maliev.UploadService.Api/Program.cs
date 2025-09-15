using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using HealthChecks.UI.Client;
using Maliev.UploadService.Api.Configurations;
using Maliev.UploadService.Api.Middleware;
using Maliev.UploadService.Api.Models;
using Maliev.UploadService.Api.Services;
using Maliev.UploadService.Data.DbContexts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text;
using System.Threading.RateLimiting;
using Google.Cloud.Storage.V1;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

try
{
    Log.Information("Starting Clean Maliev Upload Service");

    // Load secrets.yaml
    builder.Configuration.AddYamlFile("secrets.yaml", optional: true, reloadOnChange: true);

    // Load secrets from mounted Kubernetes secrets
    var secretsPath = "/mnt/secrets";
    if (Directory.Exists(secretsPath))
    {
        builder.Configuration.AddKeyPerFile(directoryPath: secretsPath, optional: true);
    }

    // API Versioning
    builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    }).AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

    // Add controllers
    builder.Services.AddControllers();

    // Configure Upload DbContext (only for file metadata tracking)
    if (builder.Environment.IsEnvironment("Testing"))
    {
        builder.Services.AddDbContext<UploadDbContext>(options =>
            options.UseInMemoryDatabase("TestDb"));
    }
    else
    {
        builder.Services.AddDbContext<UploadDbContext>(options =>
        {
            options.UseNpgsql(builder.Configuration.GetConnectionString("UploadDbContext"));
        });
    }

    // Configure caching
    builder.Services.AddMemoryCache();

    // Configure rate limiting
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.AddPolicy("UploadPolicy", context =>
            RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 10, // Lower limit for file uploads
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 2,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 5
                }));
    });

    // Google Cloud Storage
    builder.Services.AddSingleton<StorageClient>(provider =>
    {
        try
        {
            return StorageClient.Create();
        }
        catch (Exception)
        {
            Log.Warning("Failed to create authenticated StorageClient, using default");
            return StorageClient.CreateUnauthenticated();
        }
    });

    // Configure storage service options
    builder.Services.Configure<StorageServiceOptions>(
        builder.Configuration.GetSection(StorageServiceOptions.SectionName));

    // Register clean services only - no legacy dependencies
    builder.Services.AddScoped<IGoogleCloudStorageService, GoogleCloudStorageService>();
    builder.Services.AddScoped<IFileStorageService, FileStorageService>();
    builder.Services.AddScoped<IAuthorizationService, AuthorizationService>();

    // Configure Swagger
    builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
    builder.Services.AddSwaggerGen();

    // Configure CORS
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(
            policy =>
            {
                policy.WithOrigins(
                    "https://maliev.com",
                    "https://*.maliev.com",
                    "http://maliev.com",
                    "http://*.maliev.com")
                .AllowAnyHeader()
                .AllowAnyMethod();
            });
    });

    // Configure JWT Authentication (skip in Testing environment)
    if (!builder.Environment.IsEnvironment("Testing"))
    {
        var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
        if (jwtSection.Exists())
        {
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    var jwtOptions = new JwtOptions();
                    jwtSection.Bind(jwtOptions);

                    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
                    options.SaveToken = true;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
                        ValidateIssuer = true,
                        ValidIssuer = jwtOptions.Issuer,
                        ValidateAudience = true,
                        ValidAudience = jwtOptions.Audience,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero
                    };
                });

            builder.Services.AddAuthorization();
        }
    }

    // Health checks
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<UploadDbContext>("UploadDbContext", tags: new[] { "readiness" })
        .AddCheck("Liveness Check", () => HealthCheckResult.Healthy(), tags: new[] { "liveness" });

    var app = builder.Build();

    // Configure the HTTP request pipeline
    app.UseSecurityHeaders();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger(c =>
        {
            c.RouteTemplate = "uploads/swagger/{documentName}/swagger.json";
        });
        app.UseSwaggerUI(c =>
        {
            var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
            foreach (var description in provider.ApiVersionDescriptions)
            {
                c.SwaggerEndpoint($"/uploads/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
            }
            c.RoutePrefix = "uploads/swagger";
        });
    }

    // Add security middleware
    app.UseHttpsRedirection();
    app.UseHttpMetrics();
    app.UseCors();
    app.UseRateLimiter();

    // Authentication & Authorization
    app.UseAuthentication();
    app.UseAuthorization();

    // Health checks
    app.MapGet("/uploads/liveness", () => "Healthy")
        .WithTags("Health")
        .AllowAnonymous();

    app.MapHealthChecks("/uploads/readiness", new HealthCheckOptions
    {
        Predicate = healthCheck => healthCheck.Tags.Contains("ready"),
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    })
    .WithTags("Health")
    .AllowAnonymous();

    // Prometheus metrics
    app.MapMetrics("/uploads/metrics")
        .AllowAnonymous();

    app.MapControllers()
        .RequireRateLimiting("UploadPolicy");

    // Database migration in development
    if (app.Environment.IsDevelopment())
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<UploadDbContext>();
            await context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to run database migrations during startup");
        }
    }

    Log.Information("Clean Maliev Upload Service v1.0 started successfully - Path-based architecture only");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.Information("Shut down complete");
    Log.CloseAndFlush();
}

public partial class Program { }