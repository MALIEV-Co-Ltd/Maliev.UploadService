using Maliev.UploadService.Api.Services;
using Maliev.UploadService.Data;
using Maliev.UploadService.Data.Entities;
using Maliev.UploadService.Tests.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using nClam;

namespace Maliev.UploadService.Tests.Fixtures;

public class TestWebApplicationFactory : BaseIntegrationTestFactory<Program, UploadDbContext>
{
    protected override void ConfigureAdditionalServices(IServiceCollection services)
    {
        base.ConfigureAdditionalServices(services);

        // Replace ClamAV client with mock implementation for tests
        // Remove the real ClamClient registration
        var clamClientDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IClamClient));
        if (clamClientDescriptor != null)
        {
            services.Remove(clamClientDescriptor);
        }

        // Register mock ClamClient that returns clean scan results
        var mockClamClient = new Mock<IClamClient>();
        mockClamClient
            .Setup(m => m.SendAndScanFileAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClamScanResult("stream: OK"));
        mockClamClient
            .Setup(m => m.SendAndScanFileAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClamScanResult("stream: OK"));
        mockClamClient
            .Setup(m => m.PingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        services.AddSingleton(mockClamClient.Object);

        // Replace Google Cloud Storage client and IStorageService with mock implementations
        // Remove the real StorageClient and IStorageService registrations
        var storageClientDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(Google.Cloud.Storage.V1.StorageClient));
        if (storageClientDescriptor != null)
        {
            services.Remove(storageClientDescriptor);
        }

        var storageServiceDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IStorageService));
        if (storageServiceDescriptor != null)
        {
            services.Remove(storageServiceDescriptor);
        }

        // Register mock IStorageService that simulates successful uploads
        var mockStorageService = new Mock<IStorageService>();
        mockStorageService
            .Setup(m => m.UploadFileAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream stream, string path, string contentType, bool overwrite, CancellationToken ct) =>
            {
                // Read the stream to get the file size
                var position = stream.Position;
                stream.Seek(0, SeekOrigin.End);
                var fileSize = stream.Position;
                stream.Position = position;

                return new StorageUploadResult
                {
                    StoragePath = path,
                    ContentType = contentType,
                    SizeBytes = fileSize,
                    UploadedAt = DateTime.UtcNow
                };
            });

        mockStorageService
            .Setup(m => m.FileExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        mockStorageService
            .Setup(m => m.GetFileMetadataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StorageFileMetadata?)null);

        mockStorageService
            .Setup(m => m.InitiateResumableUploadAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, string contentType, long totalSize, CancellationToken ct) =>
                new ResumableUploadSession
                {
                    SessionUri = $"https://storage.googleapis.com/upload/mock/{Guid.NewGuid()}",
                    StoragePath = path,
                    ExpiresAt = DateTime.UtcNow.AddHours(24)
                });

        mockStorageService
            .Setup(m => m.GenerateSignedUrlAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string path, TimeSpan expiration, CancellationToken ct) =>
                $"https://storage.googleapis.com/maliev-uploads/{path}?X-Goog-Signature=mock-signature&X-Goog-Expires={expiration.TotalSeconds}");

        mockStorageService
            .Setup(m => m.ResumeUploadAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string sessionUri, Stream chunk, long startByte, long endByte, long totalSize, CancellationToken ct) =>
            {
                var bytesReceived = endByte + 1; // endByte is 0-indexed
                return new ResumableUploadProgress
                {
                    BytesReceived = bytesReceived,
                    TotalSize = totalSize,
                    IsComplete = bytesReceived >= totalSize,
                    StoragePath = bytesReceived >= totalSize ? "mock-path/file.bin" : null
                };
            });

        services.AddScoped(_ => mockStorageService.Object);
    }

    protected override async Task SeedTestDataAsync()
    {
        // Seed authorization policies for test services
        await using var context = CreateDbContext();

        var policies = new[]
        {
            new ServiceAuthorizationPolicy
            {
                PolicyId = Guid.NewGuid().ToString(),
                ServiceId = "test-service",
                ServiceName = "Test Service",
                AllowedPathPrefixes = new List<string> { "test-service/" },
                AllowedContentTypes = new List<string> { "*/*" }, // Allow all content types for tests
                MaxFileSizeBytes = 1024L * 1024L * 1024L, // 1GB
                StorageQuotaBytes = 10L * 1024L * 1024L * 1024L, // 10GB
                AllowOverwrite = true,
                AllowResumableUpload = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ServiceAuthorizationPolicy
            {
                PolicyId = Guid.NewGuid().ToString(),
                ServiceId = "service-a",
                ServiceName = "Service A",
                AllowedPathPrefixes = new List<string> { "service-a/" },
                AllowedContentTypes = new List<string> { "*/*" },
                MaxFileSizeBytes = 1024L * 1024L * 1024L,
                StorageQuotaBytes = 10L * 1024L * 1024L * 1024L,
                AllowOverwrite = true,
                AllowResumableUpload = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new ServiceAuthorizationPolicy
            {
                PolicyId = Guid.NewGuid().ToString(),
                ServiceId = "service-b",
                ServiceName = "Service B",
                AllowedPathPrefixes = new List<string> { "service-b/" },
                AllowedContentTypes = new List<string> { "*/*" },
                MaxFileSizeBytes = 1024L * 1024L * 1024L,
                StorageQuotaBytes = 10L * 1024L * 1024L * 1024L,
                AllowOverwrite = true,
                AllowResumableUpload = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        context.ServiceAuthorizationPolicies.AddRange(policies);
        await context.SaveChangesAsync();
    }
}
