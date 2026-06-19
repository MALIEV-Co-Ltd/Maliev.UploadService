using Maliev.Aspire.ServiceDefaults.IAM;
using Maliev.UploadService.Domain.Entities;
using Maliev.UploadService.Infrastructure.Persistence;
using Maliev.UploadService.Api.Data;
using Maliev.UploadService.Api.Services;
using Maliev.UploadService.Api.Services.Auth;
using Maliev.UploadService.Api.Metrics;
using Maliev.UploadService.Tests.Fixtures;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics.Metrics;
using Moq;
using Xunit;

namespace Maliev.UploadService.Tests.Unit.Services;

[Collection("TestDatabase")]
public class AuthorizationPolicyServiceTests : IAsyncLifetime
{
    private readonly TestDatabaseFixture _fixture;
    private UploadDbContext? _context;

    public AuthorizationPolicyServiceTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _context = _fixture.CreateDbContext();
    }

    public async Task DisposeAsync()
    {
        if (_context != null)
        {
            await _context.Database.EnsureDeletedAsync();
            await _context.DisposeAsync();
        }
    }

    private AuthorizationPolicyService CreateService(UploadDbContext context, Mock<IIamServiceClient>? mockIamClient = null)
    {
        var mockCache = new Mock<IDistributedCache>();
        var mockLogger = new Mock<ILogger<AuthorizationPolicyService>>();
        mockIamClient ??= new Mock<IIamServiceClient>();

        var mockMeterFactory = new Mock<IMeterFactory>();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authorization:PolicyCacheDurationMinutes"] = "5",
                ["Service:Name"] = "UploadService"
            })
            .Build();

        var metrics = new UploadMetrics(mockMeterFactory.Object, configuration);

        return new AuthorizationPolicyService(context, mockCache.Object, mockLogger.Object, configuration, mockIamClient.Object, metrics);
    }

    [Fact]
    public async Task CanUploadToPathAsync_ServiceName_NormalizesIamPrincipal()
    {
        var mockIamClient = new Mock<IIamServiceClient>();
        mockIamClient
            .Setup(client => client.CheckPermissionAsync(
                "system:service:pdf",
                UploadPermissions.FilesUpload,
                "folders/pdf/invoices/file.pdf",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService(_context!, mockIamClient);

        var result = await service.CanUploadToPathAsync("PdfService", "pdf/invoices/file.pdf");

        Assert.True(result);
        mockIamClient.Verify(
            client => client.CheckPermissionAsync(
                "system:service:pdf",
                UploadPermissions.FilesUpload,
                "folders/pdf/invoices/file.pdf",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SeedSamplePoliciesAsync_WebAndQuotePoliciesAllowFbxUploads()
    {
        _context!.ServiceAuthorizationPolicies.RemoveRange(_context.ServiceAuthorizationPolicies);
        await _context.SaveChangesAsync();

        await SeedData.SeedSamplePoliciesAsync(_context!, Mock.Of<ILogger>(), isDevelopment: true);

        var service = CreateService(_context!);

        Assert.True(await service.IsContentTypeAllowedAsync("WebBff", "application/x-fbx"));
        Assert.True(await service.IsContentTypeAllowedAsync("QuoteEngine", "application/x-fbx"));
    }

    [Fact]
    public async Task MigratedQuoteEnginePolicy_AllowsCustomerDocumentUploads()
    {
        var service = CreateService(_context!);

        Assert.True(await service.CanUploadToPathAsync(
            "QuoteEngine",
            "customer-documents/customer-id/document-id/receipt.pdf"));
        Assert.True(await service.CanAccessPathAsync(
            "QuoteEngine",
            "customer-documents/customer-id/document-id/receipt.pdf"));
        Assert.True(await service.IsContentTypeAllowedAsync("QuoteEngine", "application/pdf"));
    }

    private async Task<Upload> CreateTestUploadAsync(UploadDbContext context, string uploadId, string serviceId = "test-service")
    {
        var upload = new Upload
        {
            UploadId = uploadId,
            ServiceId = serviceId,
            FileName = $"test-{uploadId}.txt",
            ContentType = "text/plain",
            FileSize = 1024,
            StoragePath = $"{serviceId}/test-{uploadId}.txt",
            Status = UploadStatus.Completed,
            UploadedAt = DateTime.UtcNow
        };
        context.Uploads.Add(upload);
        await context.SaveChangesAsync();
        return upload;
    }

    [Fact]
    public async Task CanUploadToPathAsync_ValidPathPrefix_ReturnsTrue()
    {
        _context!.ServiceAuthorizationPolicies.Add(new ServiceAuthorizationPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            ServiceId = "test-service",
            ServiceName = "Test Service",
            AllowedPathPrefixes = new List<string> { "test-service/" },
            AllowedContentTypes = new List<string> { "text/plain" },
            MaxFileSizeBytes = 100 * 1024 * 1024,
            StorageQuotaBytes = 1024L * 1024 * 1024,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var service = CreateService(_context);

        var result = await service.CanUploadToPathAsync("test-service", "test-service/uploads/file.txt");

        Assert.True(result);
    }

    [Fact]
    public async Task CanUploadToPathAsync_InvalidPathPrefix_ReturnsFalse()
    {
        _context!.ServiceAuthorizationPolicies.Add(new ServiceAuthorizationPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            ServiceId = "test-service",
            ServiceName = "Test Service",
            AllowedPathPrefixes = new List<string> { "test-service/" },
            AllowedContentTypes = new List<string> { "text/plain" },
            MaxFileSizeBytes = 100 * 1024 * 1024,
            StorageQuotaBytes = 1024L * 1024 * 1024,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var service = CreateService(_context);

        var result = await service.CanUploadToPathAsync("test-service", "other-service/uploads/file.txt");

        Assert.False(result);
    }

    [Fact]
    public async Task CanAccessPathAsync_ValidPathPrefix_ReturnsTrue()
    {
        _context!.ServiceAuthorizationPolicies.Add(new ServiceAuthorizationPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            ServiceId = "test-service",
            ServiceName = "Test Service",
            AllowedPathPrefixes = new List<string> { "test-service/" },
            AllowedContentTypes = new List<string> { "text/plain" },
            MaxFileSizeBytes = 100 * 1024 * 1024,
            StorageQuotaBytes = 1024L * 1024 * 1024,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var service = CreateService(_context);

        var result = await service.CanAccessPathAsync("test-service", "test-service/uploads/file.txt");

        Assert.True(result);
    }

    [Fact]
    public async Task CanAccessPathAsync_InvalidPathPrefix_ReturnsFalse()
    {
        _context!.ServiceAuthorizationPolicies.Add(new ServiceAuthorizationPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            ServiceId = "test-service",
            ServiceName = "Test Service",
            AllowedPathPrefixes = new List<string> { "test-service/" },
            AllowedContentTypes = new List<string> { "text/plain" },
            MaxFileSizeBytes = 100 * 1024 * 1024,
            StorageQuotaBytes = 1024L * 1024 * 1024,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var service = CreateService(_context);

        var result = await service.CanAccessPathAsync("test-service", "other-service/uploads/file.txt");

        Assert.False(result);
    }

    [Fact]
    public async Task IsContentTypeAllowedAsync_AllowedType_ReturnsTrue()
    {
        _context!.ServiceAuthorizationPolicies.Add(new ServiceAuthorizationPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            ServiceId = "test-service",
            ServiceName = "Test Service",
            AllowedPathPrefixes = new List<string> { "test-service/" },
            AllowedContentTypes = new List<string> { "text/plain", "application/pdf", "image/jpeg" },
            MaxFileSizeBytes = 100 * 1024 * 1024,
            StorageQuotaBytes = 1024L * 1024 * 1024,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var service = CreateService(_context);

        var result = await service.IsContentTypeAllowedAsync("test-service", "application/pdf");

        Assert.True(result);
    }

    [Fact]
    public async Task IsContentTypeAllowedAsync_DisallowedType_ReturnsFalse()
    {
        _context!.ServiceAuthorizationPolicies.Add(new ServiceAuthorizationPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            ServiceId = "test-service",
            ServiceName = "Test Service",
            AllowedPathPrefixes = new List<string> { "test-service/" },
            AllowedContentTypes = new List<string> { "text/plain" },
            MaxFileSizeBytes = 100 * 1024 * 1024,
            StorageQuotaBytes = 1024L * 1024 * 1024,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var service = CreateService(_context);

        var result = await service.IsContentTypeAllowedAsync("test-service", "application/exe");

        Assert.False(result);
    }

    [Fact]
    public async Task IsFileSizeAllowedAsync_WithinLimit_ReturnsTrue()
    {
        _context!.ServiceAuthorizationPolicies.Add(new ServiceAuthorizationPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            ServiceId = "test-service",
            ServiceName = "Test Service",
            AllowedPathPrefixes = new List<string> { "test-service/" },
            AllowedContentTypes = new List<string> { "text/plain" },
            MaxFileSizeBytes = 100 * 1024 * 1024,
            StorageQuotaBytes = 1024L * 1024 * 1024,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var service = CreateService(_context);

        var result = await service.IsFileSizeAllowedAsync("test-service", 50 * 1024 * 1024);

        Assert.True(result);
    }

    [Fact]
    public async Task IsFileSizeAllowedAsync_ExceedsLimit_ReturnsFalse()
    {
        _context!.ServiceAuthorizationPolicies.Add(new ServiceAuthorizationPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            ServiceId = "test-service",
            ServiceName = "Test Service",
            AllowedPathPrefixes = new List<string> { "test-service/" },
            AllowedContentTypes = new List<string> { "text/plain" },
            MaxFileSizeBytes = 100 * 1024 * 1024,
            StorageQuotaBytes = 1024L * 1024 * 1024,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var service = CreateService(_context);

        var result = await service.IsFileSizeAllowedAsync("test-service", 150 * 1024 * 1024);

        Assert.False(result);
    }

    [Fact]
    public async Task GetPolicyAsync_NonExistentService_ReturnsNull()
    {
        var service = CreateService(_context!);

        var result = await service.GetPolicyAsync("non-existent-service");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPolicyAsync_InactivePolicy_ReturnsNull()
    {
        _context!.ServiceAuthorizationPolicies.Add(new ServiceAuthorizationPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            ServiceId = "test-service",
            ServiceName = "Test Service",
            AllowedPathPrefixes = new List<string> { "test-service/" },
            AllowedContentTypes = new List<string> { "text/plain" },
            MaxFileSizeBytes = 100 * 1024 * 1024,
            StorageQuotaBytes = 1024L * 1024 * 1024,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var service = CreateService(_context);

        var result = await service.GetPolicyAsync("test-service");

        Assert.Null(result);
    }

    [Fact]
    public async Task CanOverwriteAsync_PolicyAllowsOverwrite_ReturnsTrue()
    {
        _context!.ServiceAuthorizationPolicies.Add(new ServiceAuthorizationPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            ServiceId = "test-service",
            ServiceName = "Test Service",
            AllowedPathPrefixes = new List<string> { "test-service/" },
            AllowedContentTypes = new List<string> { "text/plain" },
            MaxFileSizeBytes = 100 * 1024 * 1024,
            StorageQuotaBytes = 1024L * 1024 * 1024,
            AllowOverwrite = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var service = CreateService(_context);

        var result = await service.CanOverwriteAsync("test-service");

        Assert.True(result);
    }

    [Fact]
    public async Task CanOverwriteAsync_PolicyDisallowsOverwrite_ReturnsFalse()
    {
        _context!.ServiceAuthorizationPolicies.Add(new ServiceAuthorizationPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            ServiceId = "test-service",
            ServiceName = "Test Service",
            AllowedPathPrefixes = new List<string> { "test-service/" },
            AllowedContentTypes = new List<string> { "text/plain" },
            MaxFileSizeBytes = 100 * 1024 * 1024,
            StorageQuotaBytes = 1024L * 1024 * 1024,
            AllowOverwrite = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var service = CreateService(_context);

        var result = await service.CanOverwriteAsync("test-service");

        Assert.False(result);
    }

    [Fact]
    public async Task CanOverwriteAsync_NoPolicy_ReturnsFalse()
    {
        var service = CreateService(_context!);

        var result = await service.CanOverwriteAsync("non-existent-service");

        Assert.False(result);
    }

    [Fact]
    public async Task HasStorageQuotaAsync_WithinQuota_ReturnsTrue()
    {
        var uploadId = Guid.NewGuid().ToString();
        _context!.Uploads.Add(new Upload
        {
            UploadId = uploadId,
            ServiceId = "test-service",
            FileName = "file1.txt",
            ContentType = "text/plain",
            FileSize = 100 * 1024 * 1024,
            StoragePath = "test-service/file1.txt",
            Status = UploadStatus.Completed,
            UploadedAt = DateTime.UtcNow
        });

        _context.ServiceAuthorizationPolicies.Add(new ServiceAuthorizationPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            ServiceId = "test-service",
            ServiceName = "Test Service",
            AllowedPathPrefixes = new List<string> { "test-service/" },
            AllowedContentTypes = new List<string> { "text/plain" },
            MaxFileSizeBytes = 100 * 1024 * 1024,
            StorageQuotaBytes = 1024L * 1024 * 1024,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        _context.FileMetadata.Add(new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            UploadId = uploadId,
            ServiceId = "test-service",
            StoragePath = "test-service/file1.txt",
            VersionETag = "etag1",
            FileSize = 100 * 1024 * 1024,
            ContentType = "text/plain",
            Checksum = "checksum1",
            UploadedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        var service = CreateService(_context);

        var result = await service.HasStorageQuotaAsync("test-service", 50 * 1024 * 1024);

        Assert.True(result);
    }

    [Fact]
    public async Task HasStorageQuotaAsync_ExceedsQuota_ReturnsFalse()
    {
        var uploadId = Guid.NewGuid().ToString();
        _context!.Uploads.Add(new Upload
        {
            UploadId = uploadId,
            ServiceId = "test-service",
            FileName = "file1.txt",
            ContentType = "text/plain",
            FileSize = 150 * 1024 * 1024,
            StoragePath = "test-service/file1.txt",
            Status = UploadStatus.Completed,
            UploadedAt = DateTime.UtcNow
        });

        _context.ServiceAuthorizationPolicies.Add(new ServiceAuthorizationPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            ServiceId = "test-service",
            ServiceName = "Test Service",
            AllowedPathPrefixes = new List<string> { "test-service/" },
            AllowedContentTypes = new List<string> { "text/plain" },
            MaxFileSizeBytes = 100 * 1024 * 1024,
            StorageQuotaBytes = 200L * 1024 * 1024,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        _context.FileMetadata.Add(new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            UploadId = uploadId,
            ServiceId = "test-service",
            StoragePath = "test-service/file1.txt",
            VersionETag = "etag1",
            FileSize = 150 * 1024 * 1024,
            ContentType = "text/plain",
            Checksum = "checksum1",
            UploadedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        var service = CreateService(_context);

        var result = await service.HasStorageQuotaAsync("test-service", 100 * 1024 * 1024);

        Assert.False(result);
    }

    [Fact]
    public async Task HasStorageQuotaAsync_NoPolicy_ReturnsFalse()
    {
        var service = CreateService(_context!);

        var result = await service.HasStorageQuotaAsync("non-existent-service", 1024);

        Assert.False(result);
    }
}
