using System.Security.Cryptography;
using System.Text;
using Maliev.UploadService.Api.Data;
using Maliev.UploadService.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

namespace Maliev.UploadService.Tests.Fixtures;

public class TestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .WithDatabase("uploadservice_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly RedisContainer _redisContainer = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder()
        .WithImage("rabbitmq:3-management-alpine")
        .Build();

    private RSA? _rsa;
    private RsaSecurityKey? _securityKey;

    public string PostgresConnectionString => _postgresContainer.GetConnectionString();
    public string RedisConnectionString => _redisContainer.GetConnectionString();
    public string RabbitMqConnectionString => _rabbitMqContainer.GetConnectionString();
    public RsaSecurityKey SecurityKey => _securityKey ?? throw new InvalidOperationException("Factory not initialized");
    public SigningCredentials SigningCredentials => new(SecurityKey, SecurityAlgorithms.RsaSha256);

    public async Task InitializeAsync()
    {
        // Generate RSA key pair for JWT testing
        _rsa = RSA.Create(2048);
        _securityKey = new RsaSecurityKey(_rsa);

        // Start containers in parallel
        await Task.WhenAll(
            _postgresContainer.StartAsync(),
            _redisContainer.StartAsync(),
            _rabbitMqContainer.StartAsync()
        );
    }

    public new async Task DisposeAsync()
    {
        _rsa?.Dispose();
        await Task.WhenAll(
            _postgresContainer.DisposeAsync().AsTask(),
            _redisContainer.DisposeAsync().AsTask(),
            _rabbitMqContainer.DisposeAsync().AsTask()
        );
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set test connection strings in configuration before services are built
        builder.UseSetting("ConnectionStrings:UploadServiceDbContext", PostgresConnectionString);
        builder.UseSetting("ConnectionStrings:redis", RedisConnectionString);
        // Use full RabbitMQ URI for testcontainer which includes random port
        builder.UseSetting("ConnectionStrings:rabbitmq", RabbitMqConnectionString);
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Replace database connection
            services.RemoveAll<DbContextOptions<UploadServiceDbContext>>();
            services.AddDbContext<UploadServiceDbContext>(options =>
                options.UseNpgsql(PostgresConnectionString));

            // Replace Redis connection
            services.RemoveAll<Microsoft.Extensions.Caching.StackExchangeRedis.RedisCacheOptions>();
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = RedisConnectionString;
            });

            // Replace GCS StorageClient with mock for testing
            services.RemoveAll<Google.Cloud.Storage.V1.StorageClient>();
            services.RemoveAll<IStorageService>();
            services.AddScoped<IStorageService>(sp =>
            {
                var mockService = new Mock<IStorageService>();

                // Setup default behavior for upload
                mockService
                    .Setup(x => x.UploadFileAsync(
                        It.IsAny<Stream>(),
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<bool>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync((Stream stream, string path, string contentType, bool overwrite, CancellationToken ct) =>
                        new StorageUploadResult
                        {
                            StoragePath = path,
                            ContentType = contentType,
                            SizeBytes = stream.Length,
                            UploadedAt = DateTime.UtcNow
                        });

                // Setup default behavior for file exists
                mockService
                    .Setup(x => x.FileExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(false);

                // Setup default behavior for delete
                mockService
                    .Setup(x => x.DeleteFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);

                // Setup default behavior for generate signed URL
                mockService
                    .Setup(x => x.GenerateSignedUrlAsync(
                        It.IsAny<string>(),
                        It.IsAny<TimeSpan>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync((string path, TimeSpan expiration, CancellationToken ct) =>
                        $"https://storage.googleapis.com/maliev-uploads/{path}?signed=test");

                // Setup default behavior for get file metadata
                mockService
                    .Setup(x => x.GetFileMetadataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((string path, CancellationToken ct) =>
                        new StorageFileMetadata
                        {
                            Name = path,
                            ContentType = "text/plain",
                            SizeBytes = 1024,
                            CreatedAt = DateTime.UtcNow,
                            ETag = "test-etag"
                        });

                // Setup default behavior for initiate resumable upload
                mockService
                    .Setup(x => x.InitiateResumableUploadAsync(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<long>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync((string path, string contentType, long totalSize, CancellationToken ct) =>
                        new ResumableUploadSession
                        {
                            SessionUri = $"https://storage.googleapis.com/upload/storage/v1/b/maliev-uploads/o?uploadType=resumable&upload_id=test-{Guid.NewGuid()}",
                            StoragePath = path,
                            ExpiresAt = DateTime.UtcNow.AddHours(1)
                        });

                // Setup default behavior for resume upload
                mockService
                    .Setup(x => x.ResumeUploadAsync(
                        It.IsAny<string>(),
                        It.IsAny<Stream>(),
                        It.IsAny<long>(),
                        It.IsAny<long>(),
                        It.IsAny<long>(),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync((string sessionUri, Stream chunk, long startByte, long endByte, long totalSize, CancellationToken ct) =>
                        new ResumableUploadProgress
                        {
                            BytesReceived = endByte + 1,
                            TotalSize = totalSize,
                            IsComplete = endByte + 1 >= totalSize,
                            StoragePath = endByte + 1 >= totalSize ? "test-uploads/file.txt" : null
                        });

                return mockService.Object;
            });

            // Replace ClamAV client with mock for testing
            services.RemoveAll<nClam.IClamClient>();
            services.AddSingleton<nClam.IClamClient>(sp =>
            {
                var mockClam = new Mock<nClam.IClamClient>();

                // Setup default behavior - return clean scan result
                mockClam
                    .Setup(x => x.SendAndScanFileAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new nClam.ClamScanResult("stream: OK"));

                return mockClam.Object;
            });

            // Configure JWT authentication with test RSA key
            services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = "https://test.maliev.com",
                    ValidAudience = "uploadservice",
                    IssuerSigningKey = _securityKey
                };
            });

            // Apply migrations and seed test data
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<UploadServiceDbContext>();
            context.Database.Migrate();

            // Seed test authorization policies
            var testServices = new[] { "test-service", "service-a", "service-b", "pagination-service", "other-service" };
            foreach (var serviceId in testServices)
            {
                if (!context.ServiceAuthorizationPolicies.Any(p => p.ServiceId == serviceId))
                {
                    context.ServiceAuthorizationPolicies.Add(new Api.Models.Entities.ServiceAuthorizationPolicy
                    {
                        PolicyId = Guid.NewGuid().ToString(),
                        ServiceId = serviceId,
                        ServiceName = $"{serviceId} Service",
                        AllowedPathPrefixes = new List<string> { $"{serviceId}/" },
                        AllowedContentTypes = new List<string> { "text/plain", "application/json", "image/png", "application/pdf", "image/jpeg" },
                        MaxFileSizeBytes = 100 * 1024 * 1024, // 100MB
                        StorageQuotaBytes = 1024L * 1024 * 1024, // 1GB
                        AllowOverwrite = true,
                        AllowResumableUpload = true,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }
            context.SaveChanges();
        });
    }
}
