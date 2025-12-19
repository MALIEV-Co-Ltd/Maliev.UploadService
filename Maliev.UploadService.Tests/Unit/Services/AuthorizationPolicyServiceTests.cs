using Maliev.UploadService.Data;
using Maliev.UploadService.Data.Entities;
using Maliev.UploadService.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.UploadService.Tests.Unit.Services;

public class AuthorizationPolicyServiceTests
{
    private AuthorizationPolicyService CreateService(UploadDbContext context)
    {
        var mockCache = new Mock<IDistributedCache>();
        var mockLogger = new Mock<ILogger<AuthorizationPolicyService>>();

        // Create a proper configuration using ConfigurationBuilder
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authorization:PolicyCacheDurationMinutes"] = "60"
            })
            .Build();

        return new AuthorizationPolicyService(context, mockCache.Object, mockLogger.Object, configuration);
    }

    [Fact]
    public async Task CanUploadToPathAsync_ValidPathPrefix_ReturnsTrue()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<UploadDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new UploadDbContext(options);
        context.ServiceAuthorizationPolicies.Add(new ServiceAuthorizationPolicy
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
        await context.SaveChangesAsync();

        var service = CreateService(context);

        // Act
        var result = await service.CanUploadToPathAsync("test-service", "test-service/uploads/file.txt");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CanUploadToPathAsync_InvalidPathPrefix_ReturnsFalse()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<UploadDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new UploadDbContext(options);
        context.ServiceAuthorizationPolicies.Add(new ServiceAuthorizationPolicy
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
        await context.SaveChangesAsync();

        var service = CreateService(context);

        // Act
        var result = await service.CanUploadToPathAsync("test-service", "other-service/uploads/file.txt");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CanAccessPathAsync_ValidPathPrefix_ReturnsTrue()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<UploadDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new UploadDbContext(options);
        context.ServiceAuthorizationPolicies.Add(new ServiceAuthorizationPolicy
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
        await context.SaveChangesAsync();

        var service = CreateService(context);

        // Act
        var result = await service.CanAccessPathAsync("test-service", "test-service/uploads/file.txt");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CanAccessPathAsync_InvalidPathPrefix_ReturnsFalse()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<UploadDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new UploadDbContext(options);
        context.ServiceAuthorizationPolicies.Add(new ServiceAuthorizationPolicy
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
        await context.SaveChangesAsync();

        var service = CreateService(context);

        // Act
        var result = await service.CanAccessPathAsync("test-service", "other-service/uploads/file.txt");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsContentTypeAllowedAsync_AllowedType_ReturnsTrue()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<UploadDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new UploadDbContext(options);
        context.ServiceAuthorizationPolicies.Add(new ServiceAuthorizationPolicy
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
        await context.SaveChangesAsync();

        var service = CreateService(context);

        // Act
        var result = await service.IsContentTypeAllowedAsync("test-service", "application/pdf");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsContentTypeAllowedAsync_DisallowedType_ReturnsFalse()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<UploadDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new UploadDbContext(options);
        context.ServiceAuthorizationPolicies.Add(new ServiceAuthorizationPolicy
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
        await context.SaveChangesAsync();

        var service = CreateService(context);

        // Act
        var result = await service.IsContentTypeAllowedAsync("test-service", "application/exe");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsFileSizeAllowedAsync_WithinLimit_ReturnsTrue()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<UploadDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new UploadDbContext(options);
        context.ServiceAuthorizationPolicies.Add(new ServiceAuthorizationPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            ServiceId = "test-service",
            ServiceName = "Test Service",
            AllowedPathPrefixes = new List<string> { "test-service/" },
            AllowedContentTypes = new List<string> { "text/plain" },
            MaxFileSizeBytes = 100 * 1024 * 1024, // 100 MB
            StorageQuotaBytes = 1024L * 1024 * 1024,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        // Act
        var result = await service.IsFileSizeAllowedAsync("test-service", 50 * 1024 * 1024); // 50 MB

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsFileSizeAllowedAsync_ExceedsLimit_ReturnsFalse()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<UploadDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new UploadDbContext(options);
        context.ServiceAuthorizationPolicies.Add(new ServiceAuthorizationPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            ServiceId = "test-service",
            ServiceName = "Test Service",
            AllowedPathPrefixes = new List<string> { "test-service/" },
            AllowedContentTypes = new List<string> { "text/plain" },
            MaxFileSizeBytes = 100 * 1024 * 1024, // 100 MB
            StorageQuotaBytes = 1024L * 1024 * 1024,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        // Act
        var result = await service.IsFileSizeAllowedAsync("test-service", 150 * 1024 * 1024); // 150 MB

        // Assert
        Assert.False(result);
    }


    [Fact]
    public async Task GetPolicyAsync_NonExistentService_ReturnsNull()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<UploadDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new UploadDbContext(options);
        var service = CreateService(context);

        // Act
        var result = await service.GetPolicyAsync("non-existent-service");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetPolicyAsync_InactivePolicy_ReturnsNull()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<UploadDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new UploadDbContext(options);
        context.ServiceAuthorizationPolicies.Add(new ServiceAuthorizationPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            ServiceId = "test-service",
            ServiceName = "Test Service",
            AllowedPathPrefixes = new List<string> { "test-service/" },
            AllowedContentTypes = new List<string> { "text/plain" },
            MaxFileSizeBytes = 100 * 1024 * 1024,
            StorageQuotaBytes = 1024L * 1024 * 1024,
            IsActive = false, // Inactive
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        // Act
        var result = await service.GetPolicyAsync("test-service");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CanOverwriteAsync_PolicyAllowsOverwrite_ReturnsTrue()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<UploadDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new UploadDbContext(options);
        context.ServiceAuthorizationPolicies.Add(new ServiceAuthorizationPolicy
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
        await context.SaveChangesAsync();

        var service = CreateService(context);

        // Act
        var result = await service.CanOverwriteAsync("test-service");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CanOverwriteAsync_PolicyDisallowsOverwrite_ReturnsFalse()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<UploadDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new UploadDbContext(options);
        context.ServiceAuthorizationPolicies.Add(new ServiceAuthorizationPolicy
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
        await context.SaveChangesAsync();

        var service = CreateService(context);

        // Act
        var result = await service.CanOverwriteAsync("test-service");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task CanOverwriteAsync_NoPolicy_ReturnsFalse()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<UploadDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new UploadDbContext(options);
        var service = CreateService(context);

        // Act
        var result = await service.CanOverwriteAsync("non-existent-service");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task HasStorageQuotaAsync_WithinQuota_ReturnsTrue()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<UploadDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new UploadDbContext(options);
        context.ServiceAuthorizationPolicies.Add(new ServiceAuthorizationPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            ServiceId = "test-service",
            ServiceName = "Test Service",
            AllowedPathPrefixes = new List<string> { "test-service/" },
            AllowedContentTypes = new List<string> { "text/plain" },
            MaxFileSizeBytes = 100 * 1024 * 1024,
            StorageQuotaBytes = 1024L * 1024 * 1024, // 1 GB quota
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // Add existing files (100 MB total)
        context.FileMetadata.Add(new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            UploadId = Guid.NewGuid().ToString(),
            ServiceId = "test-service",
            StoragePath = "test-service/file1.txt",
            VersionETag = "etag1",
            FileSize = 100 * 1024 * 1024, // 100 MB
            ContentType = "text/plain",
            Checksum = "checksum1",
            UploadedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var service = CreateService(context);

        // Act - Try to upload 50 MB (total would be 150 MB, well within 1 GB quota)
        var result = await service.HasStorageQuotaAsync("test-service", 50 * 1024 * 1024);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task HasStorageQuotaAsync_ExceedsQuota_ReturnsFalse()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<UploadDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new UploadDbContext(options);
        context.ServiceAuthorizationPolicies.Add(new ServiceAuthorizationPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            ServiceId = "test-service",
            ServiceName = "Test Service",
            AllowedPathPrefixes = new List<string> { "test-service/" },
            AllowedContentTypes = new List<string> { "text/plain" },
            MaxFileSizeBytes = 100 * 1024 * 1024,
            StorageQuotaBytes = 200L * 1024 * 1024, // 200 MB quota
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // Add existing files (150 MB total)
        context.FileMetadata.Add(new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            UploadId = Guid.NewGuid().ToString(),
            ServiceId = "test-service",
            StoragePath = "test-service/file1.txt",
            VersionETag = "etag1",
            FileSize = 150 * 1024 * 1024, // 150 MB
            ContentType = "text/plain",
            Checksum = "checksum1",
            UploadedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var service = CreateService(context);

        // Act - Try to upload 100 MB (total would be 250 MB, exceeding 200 MB quota)
        var result = await service.HasStorageQuotaAsync("test-service", 100 * 1024 * 1024);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task HasStorageQuotaAsync_NoPolicy_ReturnsFalse()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<UploadDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new UploadDbContext(options);
        var service = CreateService(context);

        // Act
        var result = await service.HasStorageQuotaAsync("non-existent-service", 1024);

        // Assert
        Assert.False(result);
    }
}
