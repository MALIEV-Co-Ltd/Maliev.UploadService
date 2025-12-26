using Maliev.Aspire.ServiceDefaults.IAM;
using Maliev.UploadService.Data;
using Maliev.UploadService.Data.Entities;
using Maliev.UploadService.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.UploadService.Tests.Unit.Services;

/// <summary>
/// T129: Unit tests for GCS lifecycle rule creation
/// </summary>
public class LifecycleManagementServiceTests
{
    private readonly Mock<ILogger<LifecycleManagementService>> _loggerMock;
    private readonly DbContextOptions<UploadDbContext> _dbOptions;

    public LifecycleManagementServiceTests()
    {
        _loggerMock = new Mock<ILogger<LifecycleManagementService>>();

        _dbOptions = new DbContextOptionsBuilder<UploadDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    [Fact]
    public async Task ApplyRetentionPolicyAsync_WithValidPolicy_CalculatesExpirationDate()
    {
        // Arrange
        using var context = new UploadDbContext(_dbOptions);
        var service = new LifecycleManagementService(context, _loggerMock.Object);

        var policy = new RetentionPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            PolicyName = "30-day-retention",
            RetentionDays = 30,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.RetentionPolicies.Add(policy);
        await context.SaveChangesAsync();

        var fileMetadata = new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            UploadId = Guid.NewGuid().ToString(),
            ServiceId = "test-service",
            StoragePath = "test-service/file.txt",
            VersionETag = "etag",
            FileSize = 1024,
            ContentType = "text/plain",
            Checksum = "checksum",
            UploadedAt = DateTime.UtcNow
        };

        // Act
        var expiresAt = await service.ApplyRetentionPolicyAsync(fileMetadata, policy.PolicyId, CancellationToken.None);

        // Assert
        Assert.NotNull(expiresAt);
        Assert.True(expiresAt > DateTime.UtcNow.AddDays(29));
        Assert.True(expiresAt <= DateTime.UtcNow.AddDays(31)); // Allow some buffer
    }

    [Fact]
    public async Task ApplyRetentionPolicyAsync_WithIndefiniteRetention_ReturnsNull()
    {
        // Arrange
        using var context = new UploadDbContext(_dbOptions);
        var service = new LifecycleManagementService(context, _loggerMock.Object);

        var policy = new RetentionPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            PolicyName = "indefinite",
            RetentionDays = 0, // Indefinite
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.RetentionPolicies.Add(policy);
        await context.SaveChangesAsync();

        var fileMetadata = new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            UploadId = Guid.NewGuid().ToString(),
            ServiceId = "test-service",
            StoragePath = "test-service/file.txt",
            VersionETag = "etag",
            FileSize = 1024,
            ContentType = "text/plain",
            Checksum = "checksum",
            UploadedAt = DateTime.UtcNow
        };

        // Act
        var expiresAt = await service.ApplyRetentionPolicyAsync(fileMetadata, policy.PolicyId, CancellationToken.None);

        // Assert
        Assert.Null(expiresAt);
    }

    [Fact]
    public async Task GetActiveRetentionPolicyAsync_WithMatchingServiceAndPath_ReturnsPolicy()
    {
        // Arrange
        using var context = new UploadDbContext(_dbOptions);
        var service = new LifecycleManagementService(context, _loggerMock.Object);

        var policy = new RetentionPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            PolicyName = "service-policy",
            ServiceId = "test-service",
            RetentionDays = 7,
            ApplyToPathPrefix = "test-service/documents/",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.RetentionPolicies.Add(policy);
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetActiveRetentionPolicyAsync("test-service", "test-service/documents/file.pdf", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(policy.PolicyId, result.PolicyId);
    }

    [Fact]
    public async Task GetActiveRetentionPolicyAsync_WithNoMatchingPath_ReturnsNull()
    {
        // Arrange
        using var context = new UploadDbContext(_dbOptions);
        var service = new LifecycleManagementService(context, _loggerMock.Object);

        var policy = new RetentionPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            PolicyName = "service-policy",
            ServiceId = "test-service",
            RetentionDays = 7,
            ApplyToPathPrefix = "test-service/documents/",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.RetentionPolicies.Add(policy);
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetActiveRetentionPolicyAsync("test-service", "test-service/images/photo.jpg", CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetStorageClassForAge_WithTransitions_ReturnsCorrectClass()
    {
        // Arrange
        using var context = new UploadDbContext(_dbOptions);
        var service = new LifecycleManagementService(context, _loggerMock.Object);

        var transitions = new List<StorageClassTransition>
        {
            new StorageClassTransition { Days = 30, StorageClass = "NEARLINE" },
            new StorageClassTransition { Days = 90, StorageClass = "COLDLINE" },
            new StorageClassTransition { Days = 365, StorageClass = "ARCHIVE" }
        };

        // Act & Assert
        Assert.Equal("STANDARD", service.GetStorageClassForAge(10, transitions));
        Assert.Equal("NEARLINE", service.GetStorageClassForAge(45, transitions));
        Assert.Equal("COLDLINE", service.GetStorageClassForAge(120, transitions));
        Assert.Equal("ARCHIVE", service.GetStorageClassForAge(400, transitions));
    }

    [Fact]
    public async Task ApplyRetentionPolicyAsync_WithInactivePolicy_ReturnsNull()
    {
        // Arrange
        using var context = new UploadDbContext(_dbOptions);
        var service = new LifecycleManagementService(context, _loggerMock.Object);

        var policy = new RetentionPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            PolicyName = "inactive-policy",
            RetentionDays = 30,
            IsActive = false, // Inactive
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.RetentionPolicies.Add(policy);
        await context.SaveChangesAsync();

        var fileMetadata = new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            UploadId = Guid.NewGuid().ToString(),
            ServiceId = "test-service",
            StoragePath = "test-service/file.txt",
            VersionETag = "etag",
            FileSize = 1024,
            ContentType = "text/plain",
            Checksum = "checksum",
            UploadedAt = DateTime.UtcNow
        };

        // Act
        var expiresAt = await service.ApplyRetentionPolicyAsync(fileMetadata, policy.PolicyId, CancellationToken.None);

        // Assert
        Assert.Null(expiresAt);
    }

    [Fact]
    public async Task ApplyRetentionPolicyAsync_WithNonExistentPolicy_ReturnsNull()
    {
        // Arrange
        using var context = new UploadDbContext(_dbOptions);
        var service = new LifecycleManagementService(context, _loggerMock.Object);

        var fileMetadata = new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            UploadId = Guid.NewGuid().ToString(),
            ServiceId = "test-service",
            StoragePath = "test-service/file.txt",
            VersionETag = "etag",
            FileSize = 1024,
            ContentType = "text/plain",
            Checksum = "checksum",
            UploadedAt = DateTime.UtcNow
        };

        // Act
        var expiresAt = await service.ApplyRetentionPolicyAsync(fileMetadata, "non-existent-policy", CancellationToken.None);

        // Assert
        Assert.Null(expiresAt);
    }

    [Fact]
    public async Task GetActiveRetentionPolicyAsync_WithGlobalPolicy_ReturnsPolicy()
    {
        // Arrange
        using var context = new UploadDbContext(_dbOptions);
        var service = new LifecycleManagementService(context, _loggerMock.Object);

        var globalPolicy = new RetentionPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            PolicyName = "global-policy",
            ServiceId = null, // Global policy
            RetentionDays = 90,
            ApplyToPathPrefix = null, // Applies to all paths
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.RetentionPolicies.Add(globalPolicy);
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetActiveRetentionPolicyAsync("any-service", "any/path/file.txt", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(globalPolicy.PolicyId, result.PolicyId);
    }

    [Fact]
    public async Task GetActiveRetentionPolicyAsync_PrefersServiceSpecificPolicy()
    {
        // Arrange
        using var context = new UploadDbContext(_dbOptions);
        var service = new LifecycleManagementService(context, _loggerMock.Object);

        var globalPolicy = new RetentionPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            PolicyName = "global-policy",
            ServiceId = null,
            RetentionDays = 90,
            ApplyToPathPrefix = null,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var servicePolicy = new RetentionPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            PolicyName = "service-specific",
            ServiceId = "test-service",
            RetentionDays = 30,
            ApplyToPathPrefix = "test-service/",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.RetentionPolicies.AddRange(globalPolicy, servicePolicy);
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetActiveRetentionPolicyAsync("test-service", "test-service/file.txt", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(servicePolicy.PolicyId, result.PolicyId);
    }

    [Fact]
    public async Task GetActiveRetentionPolicyAsync_PrefersMoreSpecificPath()
    {
        // Arrange
        using var context = new UploadDbContext(_dbOptions);
        var service = new LifecycleManagementService(context, _loggerMock.Object);

        var broadPolicy = new RetentionPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            PolicyName = "broad-policy",
            ServiceId = "test-service",
            RetentionDays = 90,
            ApplyToPathPrefix = "test-service/",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var specificPolicy = new RetentionPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            PolicyName = "specific-policy",
            ServiceId = "test-service",
            RetentionDays = 30,
            ApplyToPathPrefix = "test-service/documents/",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.RetentionPolicies.AddRange(broadPolicy, specificPolicy);
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetActiveRetentionPolicyAsync("test-service", "test-service/documents/file.pdf", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(specificPolicy.PolicyId, result.PolicyId);
    }

    [Fact]
    public void GetStorageClassForAge_WithNoTransitions_ReturnsStandard()
    {
        // Arrange
        using var context = new UploadDbContext(_dbOptions);
        var service = new LifecycleManagementService(context, _loggerMock.Object);

        // Act
        var result = service.GetStorageClassForAge(100, new List<StorageClassTransition>());

        // Assert
        Assert.Equal("STANDARD", result);
    }

    [Fact]
    public void GetStorageClassForAge_WithNullTransitions_ReturnsStandard()
    {
        // Arrange
        using var context = new UploadDbContext(_dbOptions);
        var service = new LifecycleManagementService(context, _loggerMock.Object);

        // Act
        var result = service.GetStorageClassForAge(100, null);

        // Assert
        Assert.Equal("STANDARD", result);
    }

    [Fact]
    public async Task ProcessExpiredFilesAsync_WithExpiredFiles_ReturnsCount()
    {
        // Arrange
        using var context = new UploadDbContext(_dbOptions);
        var service = new LifecycleManagementService(context, _loggerMock.Object);

        // Add expired files
        var expiredFile1 = new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            UploadId = Guid.NewGuid().ToString(),
            ServiceId = "test-service",
            StoragePath = "test-service/expired1.txt",
            VersionETag = "etag1",
            FileSize = 1024,
            ContentType = "text/plain",
            Checksum = "checksum1",
            UploadedAt = DateTime.UtcNow.AddDays(-60),
            ExpiresAt = DateTime.UtcNow.AddDays(-1) // Expired yesterday
        };

        var expiredFile2 = new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            UploadId = Guid.NewGuid().ToString(),
            ServiceId = "test-service",
            StoragePath = "test-service/expired2.txt",
            VersionETag = "etag2",
            FileSize = 2048,
            ContentType = "text/plain",
            Checksum = "checksum2",
            UploadedAt = DateTime.UtcNow.AddDays(-90),
            ExpiresAt = DateTime.UtcNow.AddDays(-10) // Expired 10 days ago
        };

        // Add a non-expired file
        var activeFile = new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            UploadId = Guid.NewGuid().ToString(),
            ServiceId = "test-service",
            StoragePath = "test-service/active.txt",
            VersionETag = "etag3",
            FileSize = 512,
            ContentType = "text/plain",
            Checksum = "checksum3",
            UploadedAt = DateTime.UtcNow.AddDays(-10),
            ExpiresAt = DateTime.UtcNow.AddDays(20) // Expires in the future
        };

        context.FileMetadata.AddRange(expiredFile1, expiredFile2, activeFile);
        await context.SaveChangesAsync();

        // Act
        var count = await service.ProcessExpiredFilesAsync();

        // Assert
        Assert.Equal(2, count); // Should find 2 expired files
    }

    [Fact]
    public async Task ProcessExpiredFilesAsync_WithNoExpiredFiles_ReturnsZero()
    {
        // Arrange
        using var context = new UploadDbContext(_dbOptions);
        var service = new LifecycleManagementService(context, _loggerMock.Object);

        // Add only non-expired files
        var activeFile = new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            UploadId = Guid.NewGuid().ToString(),
            ServiceId = "test-service",
            StoragePath = "test-service/active.txt",
            VersionETag = "etag",
            FileSize = 512,
            ContentType = "text/plain",
            Checksum = "checksum",
            UploadedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30) // Expires in the future
        };

        context.FileMetadata.Add(activeFile);
        await context.SaveChangesAsync();

        // Act
        var count = await service.ProcessExpiredFilesAsync();

        // Assert
        Assert.Equal(0, count); // Should find 0 expired files
    }

    [Fact]
    public async Task UpdateStorageClassesAsync_WithFilesNeedingTransition_UpdatesAndReturnsCount()
    {
        // Arrange
        using var context = new UploadDbContext(_dbOptions);
        var service = new LifecycleManagementService(context, _loggerMock.Object);

        // Create retention policy with storage class transitions
        var policy = new RetentionPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            PolicyName = "transition-policy",
            RetentionDays = 365,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            StorageClassTransitions = new List<StorageClassTransition>
            {
                new StorageClassTransition { Days = 30, StorageClass = "NEARLINE" },
                new StorageClassTransition { Days = 90, StorageClass = "COLDLINE" }
            }
        };

        context.RetentionPolicies.Add(policy);
        await context.SaveChangesAsync();

        // Add file that should transition from STANDARD to NEARLINE (45 days old)
        var fileNeedingTransition = new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            UploadId = Guid.NewGuid().ToString(),
            ServiceId = "test-service",
            StoragePath = "test-service/old-file.txt",
            VersionETag = "etag",
            FileSize = 1024,
            ContentType = "text/plain",
            Checksum = "checksum",
            UploadedAt = DateTime.UtcNow.AddDays(-45), // 45 days old
            StorageClass = "STANDARD", // Current class
            RetentionPolicyId = policy.PolicyId
        };

        context.FileMetadata.Add(fileNeedingTransition);
        await context.SaveChangesAsync();

        // Act
        var count = await service.UpdateStorageClassesAsync();

        // Assert
        Assert.Equal(1, count);

        // Verify storage class was updated
        var updatedFile = await context.FileMetadata.FindAsync(fileNeedingTransition.FileId);
        Assert.Equal("NEARLINE", updatedFile!.StorageClass);
    }

    [Fact]
    public async Task UpdateStorageClassesAsync_WithFilesAlreadyInCorrectClass_ReturnsZero()
    {
        // Arrange
        using var context = new UploadDbContext(_dbOptions);
        var service = new LifecycleManagementService(context, _loggerMock.Object);

        // Create retention policy with storage class transitions
        var policy = new RetentionPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            PolicyName = "transition-policy",
            RetentionDays = 365,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            StorageClassTransitions = new List<StorageClassTransition>
            {
                new StorageClassTransition { Days = 30, StorageClass = "NEARLINE" }
            }
        };

        context.RetentionPolicies.Add(policy);
        await context.SaveChangesAsync();

        // Add file that's already in the correct storage class
        var fileInCorrectClass = new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            UploadId = Guid.NewGuid().ToString(),
            ServiceId = "test-service",
            StoragePath = "test-service/file.txt",
            VersionETag = "etag",
            FileSize = 1024,
            ContentType = "text/plain",
            Checksum = "checksum",
            UploadedAt = DateTime.UtcNow.AddDays(-45), // 45 days old
            StorageClass = "NEARLINE", // Already in correct class
            RetentionPolicyId = policy.PolicyId
        };

        context.FileMetadata.Add(fileInCorrectClass);
        await context.SaveChangesAsync();

        // Act
        var count = await service.UpdateStorageClassesAsync();

        // Assert
        Assert.Equal(0, count); // No updates needed
    }

    [Fact]
    public async Task UpdateStorageClassesAsync_WithNoTransitions_ReturnsZero()
    {
        // Arrange
        using var context = new UploadDbContext(_dbOptions);
        var service = new LifecycleManagementService(context, _loggerMock.Object);

        // Create retention policy without storage class transitions
        var policy = new RetentionPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            PolicyName = "simple-policy",
            RetentionDays = 365,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            StorageClassTransitions = new List<StorageClassTransition>() // No transitions
        };

        context.RetentionPolicies.Add(policy);
        await context.SaveChangesAsync();

        // Add file with this policy
        var file = new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            UploadId = Guid.NewGuid().ToString(),
            ServiceId = "test-service",
            StoragePath = "test-service/file.txt",
            VersionETag = "etag",
            FileSize = 1024,
            ContentType = "text/plain",
            Checksum = "checksum",
            UploadedAt = DateTime.UtcNow.AddDays(-100),
            StorageClass = "STANDARD",
            RetentionPolicyId = policy.PolicyId
        };

        context.FileMetadata.Add(file);
        await context.SaveChangesAsync();

        // Act
        var count = await service.UpdateStorageClassesAsync();

        // Assert
        Assert.Equal(0, count); // No transitions defined
    }
}


