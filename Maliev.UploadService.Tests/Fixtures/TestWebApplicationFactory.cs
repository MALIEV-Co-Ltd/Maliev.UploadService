using Maliev.Aspire.ServiceDefaults.IAM;
using Maliev.UploadService.Api.Services;
using Maliev.UploadService.Domain.Entities;
using Maliev.UploadService.Infrastructure.Persistence;
using Maliev.UploadService.Tests.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Maliev.UploadService.Tests.Fixtures;

public class TestWebApplicationFactory : BaseIntegrationTestFactory<Program, UploadDbContext>
{
    protected override void ConfigureAdditionalServices(IServiceCollection services)
    {
        base.ConfigureAdditionalServices(services);

        // Authorization infrastructure is registered by AddJwtAuthentication() via AddPermissionAuthorization()
        // in Program.cs — no manual re-registration needed in tests.

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
                    UploadedAt = DateTime.UtcNow,
                    ETag = "mock-etag"
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

        // Register default permissive IIamServiceClient mock
        // Tests that need restrictive IAM behavior should override via .WithWebHostBuilder()
        var iamClientDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IIamServiceClient));
        if (iamClientDescriptor != null)
        {
            services.Remove(iamClientDescriptor);
        }

        var mockIamClient = new Mock<IIamServiceClient>();
        // Default: allow all permissions (tests override for restrictive scenarios)
        mockIamClient.Setup(m => m.CheckPermissionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        services.AddScoped(_ => mockIamClient.Object);
    }

    protected override async Task SeedTestDataAsync()
    {
        // Seed authorization policies for test services
        await using var context = CreateDbContext();

        // Ensure idempotency: only seed if the table is empty
        if (await context.ServiceAuthorizationPolicies.AnyAsync())
        {
            return;
        }

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

    public HttpClient CreateAuthenticatedClientWithAllPermissions(string userId = "test-user")
    {
        var claims = new List<System.Security.Claims.Claim>
        {
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, userId),
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("permission", "upload.files.upload"),
            new("permission", "upload.files.download"),
            new("permission", "upload.files.read"),
            new("permission", "upload.files.delete"),
            new("permission", "upload.files.list"),
            new("permission", "upload.admin.manage-policies"),
            new("permission", "upload.admin.bulk-delete"),
            new("permission", "upload.admin.view-metrics"),
            new("permission", "upload.retention.configure"),
            new("permission", "upload.retention.execute")
        };

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: "test-issuer",
            audience: "test-audience",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: SigningCredentials
        );

        var tokenString = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokenString}");
        return client;
    }
}
