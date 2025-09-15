using FluentAssertions;
using Maliev.UploadService.Api.Services;
using Maliev.UploadService.Data.DbContexts;
using Maliev.UploadService.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Maliev.UploadService.Tests.Services;

public class AuthorizationServiceTests : IDisposable
{
    private readonly UploadDbContext _context;
    private readonly Mock<ILogger<AuthorizationService>> _mockLogger;
    private readonly AuthorizationService _authorizationService;

    public AuthorizationServiceTests()
    {
        var options = new DbContextOptionsBuilder<UploadDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new UploadDbContext(options);

        _mockLogger = new Mock<ILogger<AuthorizationService>>();
        _authorizationService = new AuthorizationService(_context, _mockLogger.Object);
    }

    [Theory]
    [InlineData(AccessLevel.Public, new[] { "Guest" }, true)]
    [InlineData(AccessLevel.Public, new string[0], true)]
    [InlineData(AccessLevel.Internal, new[] { "Guest" }, true)]
    [InlineData(AccessLevel.Internal, new string[0], false)]
    [InlineData(AccessLevel.Restricted, new[] { "Sales" }, true)]
    [InlineData(AccessLevel.Restricted, new[] { "Guest" }, false)]
    [InlineData(AccessLevel.Confidential, new[] { "Manager" }, true)]
    [InlineData(AccessLevel.Confidential, new[] { "Sales" }, false)]
    [InlineData(AccessLevel.Public, new[] { "Customer" }, true)]
    public async Task CanAccessFileAsync_DifferentAccessLevelsAndRoles_ReturnsCorrectResult(
        AccessLevel accessLevel, string[] userRoles, bool expectedResult)
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var uploadedFile = new UploadedFile
        {
            Id = fileId,
            Category = "quotations",
            EntityId = "QUO-001",
            OriginalFileName = "test.pdf",
            UploadedBy = "different-user",
            AccessLevel = accessLevel,
            ContentType = "application/pdf",
            FileSize = 1024,
            Bucket = "test-bucket",
            ObjectName = "test/path"
        };

        _context.UploadedFiles.Add(uploadedFile);
        await _context.SaveChangesAsync();

        // Act
        var result = await _authorizationService.CanAccessFileAsync(fileId, "test-user", userRoles);

        // Assert
        result.Should().Be(expectedResult);
    }

    [Fact]
    public async Task CanAccessFileAsync_SuperAdmin_AlwaysReturnsTrue()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var uploadedFile = new UploadedFile
        {
            Id = fileId,
            Category = "quotations",
            EntityId = "QUO-001",
            OriginalFileName = "test.pdf",
            UploadedBy = "different-user",
            AccessLevel = AccessLevel.Confidential,
            ContentType = "application/pdf",
            FileSize = 1024,
            Bucket = "test-bucket",
            ObjectName = "test/path"
        };

        _context.UploadedFiles.Add(uploadedFile);
        await _context.SaveChangesAsync();

        // Act
        var result = await _authorizationService.CanAccessFileAsync(fileId, "test-user", new[] { "SuperAdmin" });

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessFileAsync_FileOwner_AlwaysReturnsTrue()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var uploadedFile = new UploadedFile
        {
            Id = fileId,
            Category = "quotations",
            EntityId = "QUO-001",
            OriginalFileName = "test.pdf",
            UploadedBy = "test-user", // Same as requesting user
            AccessLevel = AccessLevel.Confidential,
            ContentType = "application/pdf",
            FileSize = 1024,
            Bucket = "test-bucket",
            ObjectName = "test/path"
        };

        _context.UploadedFiles.Add(uploadedFile);
        await _context.SaveChangesAsync();

        // Act
        var result = await _authorizationService.CanAccessFileAsync(fileId, "test-user", new[] { "Guest" });

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessFileAsync_NonExistentFile_ReturnsFalse()
    {
        // Arrange
        var nonExistentFileId = Guid.NewGuid();

        // Act
        var result = await _authorizationService.CanAccessFileAsync(nonExistentFileId, "test-user", new[] { "SuperAdmin" });

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanAccessFileAsync_DeletedFile_ReturnsFalse()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var uploadedFile = new UploadedFile
        {
            Id = fileId,
            Category = "quotations",
            EntityId = "QUO-001",
            OriginalFileName = "test.pdf",
            UploadedBy = "test-user",
            AccessLevel = AccessLevel.Public,
            IsDeleted = true, // File is deleted
            ContentType = "application/pdf",
            FileSize = 1024,
            Bucket = "test-bucket",
            ObjectName = "test/path"
        };

        _context.UploadedFiles.Add(uploadedFile);
        await _context.SaveChangesAsync();

        // Act
        var result = await _authorizationService.CanAccessFileAsync(fileId, "test-user", new[] { "SuperAdmin" });

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("quotations", new[] { "Sales" }, true)]
    [InlineData("quotations", new[] { "Guest" }, false)]
    [InlineData("invoices", new[] { "Finance" }, true)]
    [InlineData("invoices", new[] { "Sales" }, false)]
    [InlineData("receipts", new[] { "Finance" }, true)]
    [InlineData("customers", new[] { "Sales" }, true)]
    [InlineData("orders", new[] { "Production" }, true)]
    [InlineData("products", new[] { "Production" }, true)]
    [InlineData("finance", new[] { "Finance" }, true)]
    [InlineData("marketing", new[] { "Sales" }, true)]
    [InlineData("legal", new[] { "Manager" }, true)]
    [InlineData("legal", new[] { "Sales" }, false)]
    [InlineData("temp", new[] { "Guest" }, true)]
    public async Task CanUploadToCategory_DifferentCategoriesAndRoles_ReturnsCorrectResult(
        string category, string[] userRoles, bool expectedResult)
    {
        // Act
        var result = await _authorizationService.CanUploadToCategory(category, "test-user", userRoles);

        // Assert
        result.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(new[] { "SuperAdmin" }, true)]
    [InlineData(new[] { "Admin" }, true)]
    [InlineData(new[] { "Manager" }, true)]
    [InlineData(new[] { "Sales" }, true)]
    public async Task CanUploadToCategory_HigherRoles_CanUploadToAnyCategory(string[] userRoles, bool expectedResult)
    {
        // Act
        var result = await _authorizationService.CanUploadToCategory("legal", "test-user", userRoles);

        // Assert - Only Manager and above should be able to upload to legal
        result.Should().Be(expectedResult && (userRoles.Contains("SuperAdmin") || userRoles.Contains("Admin") || userRoles.Contains("Manager")));
    }

    [Fact]
    public async Task CanDeleteFileAsync_SuperAdmin_AlwaysReturnsTrue()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var uploadedFile = new UploadedFile
        {
            Id = fileId,
            Category = "quotations",
            EntityId = "QUO-001",
            OriginalFileName = "test.pdf",
            UploadedBy = "different-user",
            AccessLevel = AccessLevel.Confidential,
            ContentType = "application/pdf",
            FileSize = 1024,
            Bucket = "test-bucket",
            ObjectName = "test/path"
        };

        _context.UploadedFiles.Add(uploadedFile);
        await _context.SaveChangesAsync();

        // Act
        var result = await _authorizationService.CanDeleteFileAsync(fileId, "test-user", new[] { "SuperAdmin" });

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanDeleteFileAsync_FileOwner_ReturnsTrue()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var uploadedFile = new UploadedFile
        {
            Id = fileId,
            Category = "quotations",
            EntityId = "QUO-001",
            OriginalFileName = "test.pdf",
            UploadedBy = "test-user", // Same as requesting user
            AccessLevel = AccessLevel.Confidential,
            ContentType = "application/pdf",
            FileSize = 1024,
            Bucket = "test-bucket",
            ObjectName = "test/path"
        };

        _context.UploadedFiles.Add(uploadedFile);
        await _context.SaveChangesAsync();

        // Act
        var result = await _authorizationService.CanDeleteFileAsync(fileId, "test-user", new[] { "Guest" });

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanDeleteFileAsync_ManagerWithNonConfidentialFile_ReturnsTrue()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var uploadedFile = new UploadedFile
        {
            Id = fileId,
            Category = "quotations",
            EntityId = "QUO-001",
            OriginalFileName = "test.pdf",
            UploadedBy = "different-user",
            AccessLevel = AccessLevel.Internal, // Non-confidential
            ContentType = "application/pdf",
            FileSize = 1024,
            Bucket = "test-bucket",
            ObjectName = "test/path"
        };

        _context.UploadedFiles.Add(uploadedFile);
        await _context.SaveChangesAsync();

        // Act
        var result = await _authorizationService.CanDeleteFileAsync(fileId, "test-user", new[] { "Manager" });

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanDeleteFileAsync_ManagerWithConfidentialFile_ReturnsFalse()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var uploadedFile = new UploadedFile
        {
            Id = fileId,
            Category = "quotations",
            EntityId = "QUO-001",
            OriginalFileName = "test.pdf",
            UploadedBy = "different-user",
            AccessLevel = AccessLevel.Confidential, // Confidential
            ContentType = "application/pdf",
            FileSize = 1024,
            Bucket = "test-bucket",
            ObjectName = "test/path"
        };

        _context.UploadedFiles.Add(uploadedFile);
        await _context.SaveChangesAsync();

        // Act
        var result = await _authorizationService.CanDeleteFileAsync(fileId, "test-user", new[] { "Manager" });

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsFileOwnerAsync_SameUser_ReturnsTrue()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var uploadedFile = new UploadedFile
        {
            Id = fileId,
            Category = "quotations",
            EntityId = "QUO-001",
            OriginalFileName = "test.pdf",
            UploadedBy = "test-user",
            AccessLevel = AccessLevel.Internal,
            ContentType = "application/pdf",
            FileSize = 1024,
            Bucket = "test-bucket",
            ObjectName = "test/path"
        };

        _context.UploadedFiles.Add(uploadedFile);
        await _context.SaveChangesAsync();

        // Act
        var result = await _authorizationService.IsFileOwnerAsync(fileId, "test-user");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsFileOwnerAsync_DifferentUser_ReturnsFalse()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var uploadedFile = new UploadedFile
        {
            Id = fileId,
            Category = "quotations",
            EntityId = "QUO-001",
            OriginalFileName = "test.pdf",
            UploadedBy = "different-user",
            AccessLevel = AccessLevel.Internal,
            ContentType = "application/pdf",
            FileSize = 1024,
            Bucket = "test-bucket",
            ObjectName = "test/path"
        };

        _context.UploadedFiles.Add(uploadedFile);
        await _context.SaveChangesAsync();

        // Act
        var result = await _authorizationService.IsFileOwnerAsync(fileId, "test-user");

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(new[] { "SuperAdmin" }, AccessLevel.Confidential)]
    [InlineData(new[] { "Admin" }, AccessLevel.Confidential)]
    [InlineData(new[] { "Manager" }, AccessLevel.Confidential)]
    [InlineData(new[] { "Finance" }, AccessLevel.Restricted)]
    [InlineData(new[] { "Sales" }, AccessLevel.Internal)]
    [InlineData(new[] { "Production" }, AccessLevel.Internal)]
    [InlineData(new[] { "Customer" }, AccessLevel.Public)]
    [InlineData(new[] { "Guest" }, AccessLevel.Public)]
    [InlineData(new string[0], AccessLevel.Public)]
    public async Task GetMaximumAccessLevelAsync_DifferentRoles_ReturnsCorrectAccessLevel(
        string[] userRoles, AccessLevel expectedAccessLevel)
    {
        // Act
        var result = await _authorizationService.GetMaximumAccessLevelAsync("test-user", userRoles);

        // Assert
        result.Should().Be(expectedAccessLevel);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}