using Maliev.UploadService.Domain.Entities;
using Maliev.UploadService.Infrastructure.Persistence;
using Maliev.UploadService.Api.Services;
using Maliev.UploadService.Tests.Fixtures;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Maliev.UploadService.Tests.Unit.Services;

[Collection("TestDatabase")]
public class LifecycleManagementServiceTests : IAsyncLifetime
{
    private readonly Mock<ILogger<LifecycleManagementService>> _loggerMock;
    private readonly Mock<IStorageService> _storageServiceMock;
    private readonly TestDatabaseFixture _fixture;
    private UploadDbContext? _context;

    public LifecycleManagementServiceTests(TestDatabaseFixture fixture)
    {
        _loggerMock = new Mock<ILogger<LifecycleManagementService>>();
        _storageServiceMock = new Mock<IStorageService>();
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

    [Fact]
    public async Task ApplyRetentionPolicyAsync_WithValidPolicy_CalculatesExpirationDate()
    {
        var service = new LifecycleManagementService(_context!, _storageServiceMock.Object, _loggerMock.Object);

        var policy = new RetentionPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            PolicyName = "30-day-retention",
            RetentionDays = 30,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context!.RetentionPolicies.Add(policy);

        var uploadId = Guid.NewGuid().ToString();
        _context.Uploads.Add(new Upload
        {
            UploadId = uploadId,
            ServiceId = "test-service",
            FileName = "file.txt",
            ContentType = "text/plain",
            FileSize = 1024,
            StoragePath = "test-service/file.txt",
            Status = UploadStatus.Completed,
            UploadedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var fileMetadata = new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            UploadId = uploadId,
            ServiceId = "test-service",
            StoragePath = "test-service/file.txt",
            VersionETag = "etag",
            FileSize = 1024,
            ContentType = "text/plain",
            Checksum = "checksum",
            UploadedAt = DateTime.UtcNow
        };

        var expiresAt = await service.ApplyRetentionPolicyAsync(fileMetadata, policy.PolicyId, CancellationToken.None);

        Assert.NotNull(expiresAt);
        Assert.True(expiresAt > DateTime.UtcNow.AddDays(29));
        Assert.True(expiresAt <= DateTime.UtcNow.AddDays(31));
    }

    [Fact]
    public async Task ApplyRetentionPolicyAsync_WithIndefiniteRetention_ReturnsNull()
    {
        var service = new LifecycleManagementService(_context!, _storageServiceMock.Object, _loggerMock.Object);

        var policy = new RetentionPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            PolicyName = "indefinite",
            RetentionDays = 0,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context!.RetentionPolicies.Add(policy);

        var uploadId = Guid.NewGuid().ToString();
        _context.Uploads.Add(new Upload
        {
            UploadId = uploadId,
            ServiceId = "test-service",
            FileName = "file.txt",
            ContentType = "text/plain",
            FileSize = 1024,
            StoragePath = "test-service/file.txt",
            Status = UploadStatus.Completed,
            UploadedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var fileMetadata = new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            UploadId = uploadId,
            ServiceId = "test-service",
            StoragePath = "test-service/file.txt",
            VersionETag = "etag",
            FileSize = 1024,
            ContentType = "text/plain",
            Checksum = "checksum",
            UploadedAt = DateTime.UtcNow
        };

        var expiresAt = await service.ApplyRetentionPolicyAsync(fileMetadata, policy.PolicyId, CancellationToken.None);

        Assert.Null(expiresAt);
    }

    [Fact]
    public async Task GetActiveRetentionPolicyAsync_WithMatchingServiceAndPath_ReturnsPolicy()
    {
        var service = new LifecycleManagementService(_context!, _storageServiceMock.Object, _loggerMock.Object);

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

        _context!.RetentionPolicies.Add(policy);
        await _context.SaveChangesAsync();

        var result = await service.GetActiveRetentionPolicyAsync("test-service", "test-service/documents/file.pdf", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(policy.PolicyId, result.PolicyId);
    }

    [Fact]
    public async Task GetActiveRetentionPolicyAsync_WithNoMatchingPath_ReturnsNull()
    {
        var service = new LifecycleManagementService(_context!, _storageServiceMock.Object, _loggerMock.Object);

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

        _context!.RetentionPolicies.Add(policy);
        await _context.SaveChangesAsync();

        var result = await service.GetActiveRetentionPolicyAsync("test-service", "test-service/images/photo.jpg", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public void GetStorageClassForAge_WithTransitions_ReturnsCorrectClass()
    {
        var service = new LifecycleManagementService(_context!, _storageServiceMock.Object, _loggerMock.Object);

        var transitions = new List<StorageClassTransition>
        {
            new StorageClassTransition { Days = 30, StorageClass = "NEARLINE" },
            new StorageClassTransition { Days = 90, StorageClass = "COLDLINE" },
            new StorageClassTransition { Days = 365, StorageClass = "ARCHIVE" }
        };

        Assert.Equal("STANDARD", service.GetStorageClassForAge(10, transitions));
        Assert.Equal("NEARLINE", service.GetStorageClassForAge(45, transitions));
        Assert.Equal("COLDLINE", service.GetStorageClassForAge(120, transitions));
        Assert.Equal("ARCHIVE", service.GetStorageClassForAge(400, transitions));
    }

    [Fact]
    public async Task ApplyRetentionPolicyAsync_WithInactivePolicy_ReturnsNull()
    {
        var service = new LifecycleManagementService(_context!, _storageServiceMock.Object, _loggerMock.Object);

        var policy = new RetentionPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            PolicyName = "inactive-policy",
            RetentionDays = 30,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context!.RetentionPolicies.Add(policy);

        var uploadId = Guid.NewGuid().ToString();
        _context.Uploads.Add(new Upload
        {
            UploadId = uploadId,
            ServiceId = "test-service",
            FileName = "file.txt",
            ContentType = "text/plain",
            FileSize = 1024,
            StoragePath = "test-service/file.txt",
            Status = UploadStatus.Completed,
            UploadedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var fileMetadata = new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            UploadId = uploadId,
            ServiceId = "test-service",
            StoragePath = "test-service/file.txt",
            VersionETag = "etag",
            FileSize = 1024,
            ContentType = "text/plain",
            Checksum = "checksum",
            UploadedAt = DateTime.UtcNow
        };

        var expiresAt = await service.ApplyRetentionPolicyAsync(fileMetadata, policy.PolicyId, CancellationToken.None);

        Assert.Null(expiresAt);
    }

    [Fact]
    public async Task ApplyRetentionPolicyAsync_WithNonExistentPolicy_ReturnsNull()
    {
        var service = new LifecycleManagementService(_context!, _storageServiceMock.Object, _loggerMock.Object);

        var uploadId = Guid.NewGuid().ToString();
        _context!.Uploads.Add(new Upload
        {
            UploadId = uploadId,
            ServiceId = "test-service",
            FileName = "file.txt",
            ContentType = "text/plain",
            FileSize = 1024,
            StoragePath = "test-service/file.txt",
            Status = UploadStatus.Completed,
            UploadedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var fileMetadata = new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            UploadId = uploadId,
            ServiceId = "test-service",
            StoragePath = "test-service/file.txt",
            VersionETag = "etag",
            FileSize = 1024,
            ContentType = "text/plain",
            Checksum = "checksum",
            UploadedAt = DateTime.UtcNow
        };

        var expiresAt = await service.ApplyRetentionPolicyAsync(fileMetadata, "non-existent-policy", CancellationToken.None);

        Assert.Null(expiresAt);
    }

    [Fact]
    public async Task GetActiveRetentionPolicyAsync_WithGlobalPolicy_ReturnsPolicy()
    {
        var service = new LifecycleManagementService(_context!, _storageServiceMock.Object, _loggerMock.Object);

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

        _context!.RetentionPolicies.Add(globalPolicy);
        await _context.SaveChangesAsync();

        var result = await service.GetActiveRetentionPolicyAsync("any-service", "any/path/file.txt", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(globalPolicy.PolicyId, result.PolicyId);
    }

    [Fact]
    public async Task GetActiveRetentionPolicyAsync_PrefersServiceSpecificPolicy()
    {
        var service = new LifecycleManagementService(_context!, _storageServiceMock.Object, _loggerMock.Object);

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

        _context!.RetentionPolicies.AddRange(globalPolicy, servicePolicy);
        await _context.SaveChangesAsync();

        var result = await service.GetActiveRetentionPolicyAsync("test-service", "test-service/file.txt", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(servicePolicy.PolicyId, result.PolicyId);
    }

    [Fact]
    public async Task GetActiveRetentionPolicyAsync_PrefersMoreSpecificPath()
    {
        var service = new LifecycleManagementService(_context!, _storageServiceMock.Object, _loggerMock.Object);

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

        _context!.RetentionPolicies.AddRange(broadPolicy, specificPolicy);
        await _context.SaveChangesAsync();

        var result = await service.GetActiveRetentionPolicyAsync("test-service", "test-service/documents/file.pdf", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(specificPolicy.PolicyId, result.PolicyId);
    }

    [Fact]
    public void GetStorageClassForAge_WithNoTransitions_ReturnsStandard()
    {
        var service = new LifecycleManagementService(_context!, _storageServiceMock.Object, _loggerMock.Object);

        var result = service.GetStorageClassForAge(100, new List<StorageClassTransition>());

        Assert.Equal("STANDARD", result);
    }

    [Fact]
    public void GetStorageClassForAge_WithNullTransitions_ReturnsStandard()
    {
        var service = new LifecycleManagementService(_context!, _storageServiceMock.Object, _loggerMock.Object);

        var result = service.GetStorageClassForAge(100, null);

        Assert.Equal("STANDARD", result);
    }

    [Fact]
    public async Task ProcessExpiredFilesAsync_WithExpiredFiles_ReturnsCount()
    {
        var service = new LifecycleManagementService(_context!, _storageServiceMock.Object, _loggerMock.Object);

        var upload1Id = Guid.NewGuid().ToString();
        var upload2Id = Guid.NewGuid().ToString();
        var upload3Id = Guid.NewGuid().ToString();

        _context!.Uploads.Add(new Upload
        {
            UploadId = upload1Id,
            ServiceId = "test-service",
            FileName = "expired1.txt",
            ContentType = "text/plain",
            FileSize = 1024,
            StoragePath = "test-service/expired1.txt",
            Status = UploadStatus.Completed,
            UploadedAt = DateTime.UtcNow.AddDays(-60)
        });

        _context.Uploads.Add(new Upload
        {
            UploadId = upload2Id,
            ServiceId = "test-service",
            FileName = "expired2.txt",
            ContentType = "text/plain",
            FileSize = 2048,
            StoragePath = "test-service/expired2.txt",
            Status = UploadStatus.Completed,
            UploadedAt = DateTime.UtcNow.AddDays(-90)
        });

        _context.Uploads.Add(new Upload
        {
            UploadId = upload3Id,
            ServiceId = "test-service",
            FileName = "active.txt",
            ContentType = "text/plain",
            FileSize = 512,
            StoragePath = "test-service/active.txt",
            Status = UploadStatus.Completed,
            UploadedAt = DateTime.UtcNow.AddDays(-10)
        });

        await _context.SaveChangesAsync();

        var expiredFile1 = new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            UploadId = upload1Id,
            ServiceId = "test-service",
            StoragePath = "test-service/expired1.txt",
            VersionETag = "etag1",
            FileSize = 1024,
            ContentType = "text/plain",
            Checksum = "checksum1",
            UploadedAt = DateTime.UtcNow.AddDays(-60),
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        };

        var expiredFile2 = new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            UploadId = upload2Id,
            ServiceId = "test-service",
            StoragePath = "test-service/expired2.txt",
            VersionETag = "etag2",
            FileSize = 2048,
            ContentType = "text/plain",
            Checksum = "checksum2",
            UploadedAt = DateTime.UtcNow.AddDays(-90),
            ExpiresAt = DateTime.UtcNow.AddDays(-10)
        };

        var activeFile = new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            UploadId = upload3Id,
            ServiceId = "test-service",
            StoragePath = "test-service/active.txt",
            VersionETag = "etag3",
            FileSize = 512,
            ContentType = "text/plain",
            Checksum = "checksum3",
            UploadedAt = DateTime.UtcNow.AddDays(-10),
            ExpiresAt = DateTime.UtcNow.AddDays(20)
        };

        _context!.FileMetadata.AddRange(expiredFile1, expiredFile2, activeFile);
        await _context.SaveChangesAsync();

        var count = await service.ProcessExpiredFilesAsync();

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task ProcessExpiredFilesAsync_WithNoExpiredFiles_ReturnsZero()
    {
        var service = new LifecycleManagementService(_context!, _storageServiceMock.Object, _loggerMock.Object);

        var uploadId = Guid.NewGuid().ToString();
        _context!.Uploads.Add(new Upload
        {
            UploadId = uploadId,
            ServiceId = "test-service",
            FileName = "active.txt",
            ContentType = "text/plain",
            FileSize = 512,
            StoragePath = "test-service/active.txt",
            Status = UploadStatus.Completed,
            UploadedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        var activeFile = new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            UploadId = uploadId,
            ServiceId = "test-service",
            StoragePath = "test-service/active.txt",
            VersionETag = "etag",
            FileSize = 512,
            ContentType = "text/plain",
            Checksum = "checksum",
            UploadedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        _context!.FileMetadata.Add(activeFile);
        await _context.SaveChangesAsync();

        var count = await service.ProcessExpiredFilesAsync();

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task UpdateStorageClassesAsync_WithFilesNeedingTransition_UpdatesAndReturnsCount()
    {
        var service = new LifecycleManagementService(_context!, _storageServiceMock.Object, _loggerMock.Object);

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

        _context!.RetentionPolicies.Add(policy);
        await _context.SaveChangesAsync();

        var uploadId = Guid.NewGuid().ToString();
        _context.Uploads.Add(new Upload
        {
            UploadId = uploadId,
            ServiceId = "test-service",
            FileName = "old-file.txt",
            ContentType = "text/plain",
            FileSize = 1024,
            StoragePath = "test-service/old-file.txt",
            Status = UploadStatus.Completed,
            UploadedAt = DateTime.UtcNow.AddDays(-45)
        });
        await _context.SaveChangesAsync();

        var fileNeedingTransition = new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            UploadId = uploadId,
            ServiceId = "test-service",
            StoragePath = "test-service/old-file.txt",
            VersionETag = "etag",
            FileSize = 1024,
            ContentType = "text/plain",
            Checksum = "checksum",
            UploadedAt = DateTime.UtcNow.AddDays(-45),
            StorageClass = "STANDARD",
            RetentionPolicyId = policy.PolicyId
        };

        _context.FileMetadata.Add(fileNeedingTransition);
        await _context.SaveChangesAsync();

        var count = await service.UpdateStorageClassesAsync();

        Assert.Equal(1, count);

        var updatedFile = await _context.FileMetadata.FindAsync(fileNeedingTransition.FileId);
        Assert.Equal("NEARLINE", updatedFile!.StorageClass);
    }

    [Fact]
    public async Task UpdateStorageClassesAsync_WithFilesAlreadyInCorrectClass_ReturnsZero()
    {
        var service = new LifecycleManagementService(_context!, _storageServiceMock.Object, _loggerMock.Object);

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

        _context!.RetentionPolicies.Add(policy);
        await _context.SaveChangesAsync();

        var uploadId = Guid.NewGuid().ToString();
        _context.Uploads.Add(new Upload
        {
            UploadId = uploadId,
            ServiceId = "test-service",
            FileName = "file.txt",
            ContentType = "text/plain",
            FileSize = 1024,
            StoragePath = "test-service/file.txt",
            Status = UploadStatus.Completed,
            UploadedAt = DateTime.UtcNow.AddDays(-45)
        });
        await _context.SaveChangesAsync();

        var fileInCorrectClass = new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            UploadId = uploadId,
            ServiceId = "test-service",
            StoragePath = "test-service/file.txt",
            VersionETag = "etag",
            FileSize = 1024,
            ContentType = "text/plain",
            Checksum = "checksum",
            UploadedAt = DateTime.UtcNow.AddDays(-45),
            StorageClass = "NEARLINE",
            RetentionPolicyId = policy.PolicyId
        };

        _context.FileMetadata.Add(fileInCorrectClass);
        await _context.SaveChangesAsync();

        var count = await service.UpdateStorageClassesAsync();

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task UpdateStorageClassesAsync_WithNoTransitions_ReturnsZero()
    {
        var service = new LifecycleManagementService(_context!, _storageServiceMock.Object, _loggerMock.Object);

        var policy = new RetentionPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            PolicyName = "simple-policy",
            RetentionDays = 365,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            StorageClassTransitions = new List<StorageClassTransition>()
        };

        _context!.RetentionPolicies.Add(policy);
        await _context.SaveChangesAsync();

        var uploadId = Guid.NewGuid().ToString();
        _context.Uploads.Add(new Upload
        {
            UploadId = uploadId,
            ServiceId = "test-service",
            FileName = "file.txt",
            ContentType = "text/plain",
            FileSize = 1024,
            StoragePath = "test-service/file.txt",
            Status = UploadStatus.Completed,
            UploadedAt = DateTime.UtcNow.AddDays(-100)
        });
        await _context.SaveChangesAsync();

        var file = new FileMetadata
        {
            FileId = Guid.NewGuid().ToString(),
            UploadId = uploadId,
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

        _context.FileMetadata.Add(file);
        await _context.SaveChangesAsync();

        var count = await service.UpdateStorageClassesAsync();

        Assert.Equal(0, count);
    }
}
