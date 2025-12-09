using Maliev.UploadService.Api.Data;
using Maliev.UploadService.Api.Models.Entities;
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
    private readonly DbContextOptions<UploadServiceDbContext> _dbOptions;

    public LifecycleManagementServiceTests()
    {
        _loggerMock = new Mock<ILogger<LifecycleManagementService>>();

        _dbOptions = new DbContextOptionsBuilder<UploadServiceDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    [Fact]
    public async Task ApplyRetentionPolicyAsync_WithValidPolicy_CalculatesExpirationDate()
    {
        // Arrange
        using var context = new UploadServiceDbContext(_dbOptions);
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
        using var context = new UploadServiceDbContext(_dbOptions);
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
        using var context = new UploadServiceDbContext(_dbOptions);
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
        using var context = new UploadServiceDbContext(_dbOptions);
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
        using var context = new UploadServiceDbContext(_dbOptions);
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
}
