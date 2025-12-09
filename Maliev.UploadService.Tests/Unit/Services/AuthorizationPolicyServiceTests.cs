using Maliev.UploadService.Api.Data;
using Maliev.UploadService.Api.Models.Entities;
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
    private AuthorizationPolicyService CreateService(UploadServiceDbContext context)
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
        var options = new DbContextOptionsBuilder<UploadServiceDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new UploadServiceDbContext(options);
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
        var options = new DbContextOptionsBuilder<UploadServiceDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new UploadServiceDbContext(options);
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
        var options = new DbContextOptionsBuilder<UploadServiceDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new UploadServiceDbContext(options);
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
        var options = new DbContextOptionsBuilder<UploadServiceDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new UploadServiceDbContext(options);
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
}
