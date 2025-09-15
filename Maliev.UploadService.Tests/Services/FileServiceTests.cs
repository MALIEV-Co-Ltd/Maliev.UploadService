using FluentAssertions;
using Maliev.UploadService.Api.Models;
using Maliev.UploadService.Api.Services;
using Maliev.UploadService.Data.DbContexts;
using DataModels = Maliev.UploadService.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Text;

namespace Maliev.UploadService.Tests.Services;

public class FileServiceTests : IDisposable
{
    private readonly Mock<IGoogleCloudStorageService> _mockStorageService;
    private readonly Mock<ILogger<FileStorageService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly UploadDbContext _context;
    private readonly IMemoryCache _memoryCache;
    private readonly FileStorageService _fileService;

    public FileServiceTests()
    {
        // Create in-memory database
        var options = new DbContextOptionsBuilder<UploadDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new UploadDbContext(options);

        // Create mocks
        _mockStorageService = new Mock<IGoogleCloudStorageService>();
        _mockLogger = new Mock<ILogger<FileStorageService>>();
        _mockConfiguration = new Mock<IConfiguration>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());

        // Setup configuration
        _mockConfiguration.Setup(c => c["ASPNETCORE_ENVIRONMENT"]).Returns("Development");

        var mockOptions = new Mock<IOptions<StorageServiceOptions>>();
        mockOptions.Setup(x => x.Value).Returns(new StorageServiceOptions());

        _fileService = new FileStorageService(
            _mockStorageService.Object,
            mockOptions.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task UploadFileAsync_ValidFile_ReturnsSuccessResponse()
    {
        // Arrange
        var fileContent = "Test file content";
        var fileBytes = Encoding.UTF8.GetBytes(fileContent);
        var mockFile = CreateMockFile("test.txt", "text/plain", fileBytes);

        var request = new FileUploadRequest
        {
            File = mockFile.Object,
            Category = "quotations",
            EntityId = "QUO-001",
            CustomerId = "CUST-001",
            AccessLevel = AccessLevel.Internal,
            Tags = new[] { "test", "quotation" }
        };

        _mockStorageService
            .Setup(s => s.CalculateHashesAsync(It.IsAny<Stream>()))
            .ReturnsAsync(("md5hash", "sha256hash"));

        _mockStorageService
            .Setup(s => s.UploadFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync("etag123");

        // Act
        var result = await _fileService.UploadFileAsync(request, "test-user", "127.0.0.1");

        // Assert
        result.Should().NotBeNull();
        result.FileId.Should().NotBeEmpty();
        result.Category.Should().Be("quotations");
        result.EntityId.Should().Be("QUO-001");
        result.Bucket.Should().Be("maliev-dev");
        result.ObjectName.Should().StartWith("business-documents/quotations/QUO-001/");
        result.ProcessingStatus.Should().Be(ProcessingStatus.Completed);

        // Verify database record
        var dbRecord = await _context.UploadedFiles.FirstOrDefaultAsync();
        dbRecord.Should().NotBeNull();
        dbRecord!.Category.Should().Be("quotations");
        dbRecord.CustomerId.Should().Be("CUST-001");
        dbRecord.UploadedBy.Should().Be("test-user");
    }

    [Fact]
    public async Task UploadFileAsync_EmptyFile_ThrowsArgumentException()
    {
        // Arrange
        var mockFile = CreateMockFile("empty.txt", "text/plain", Array.Empty<byte>());

        var request = new FileUploadRequest
        {
            File = mockFile.Object,
            Category = "temp",
            EntityId = "TEMP-001"
        };

        // Act & Assert
        await FluentActions.Invoking(() => _fileService.UploadFileAsync(request, "test-user"))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("File is empty");
    }

    [Fact]
    public async Task UploadFileAsync_FileTooLarge_ThrowsArgumentException()
    {
        // Arrange
        var largeFileSize = 101 * 1024 * 1024; // 101MB
        var largeFileBytes = new byte[largeFileSize];
        var mockFile = CreateMockFile("large.txt", "text/plain", largeFileBytes);

        var request = new FileUploadRequest
        {
            File = mockFile.Object,
            Category = "temp",
            EntityId = "TEMP-001"
        };

        // Act & Assert
        await FluentActions.Invoking(() => _fileService.UploadFileAsync(request, "test-user"))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("File size exceeds maximum allowed size of 100MB");
    }

    [Fact]
    public async Task UploadFileAsync_DangerousFileType_ThrowsArgumentException()
    {
        // Arrange
        var fileBytes = Encoding.UTF8.GetBytes("fake exe content");
        var mockFile = CreateMockFile("malware.exe", "application/octet-stream", fileBytes);

        var request = new FileUploadRequest
        {
            File = mockFile.Object,
            Category = "temp",
            EntityId = "TEMP-001"
        };

        // Act & Assert
        await FluentActions.Invoking(() => _fileService.UploadFileAsync(request, "test-user"))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("File type '.exe' is not allowed for security reasons");
    }

    [Theory]
    [InlineData("quotations", "QUO-001", null, "business-documents/quotations/QUO-001/")]
    [InlineData("invoices", "INV-001", null, "business-documents/invoices/INV-001/")]
    [InlineData("customers", "CUST-001", "contracts", "customers/CUST-001/contracts/")]
    [InlineData("orders", "ORD-001", null, "orders/ORD-001/")]
    [InlineData("finance", "FIN-001", null, "finance/")]
    [InlineData("temp", "TEMP-001", null, "temp/TEMP-001/")]
    public void GenerateObjectName_DifferentCategories_ReturnsCorrectPaths(
        string category, string entityId, string? subcategory, string expectedPrefix)
    {
        // Arrange
        var fileName = "test.pdf";

        // Use reflection to call private method
        var method = typeof(FileService).GetMethod("GenerateObjectName",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        // Act
        var result = (string)method!.Invoke(null, new object?[] { category, entityId, subcategory, fileName })!;

        // Assert
        result.Should().StartWith(expectedPrefix);
        result.Should().EndWith("_test.pdf");
        result.Should().Contain(DateTime.UtcNow.ToString("yyyyMMdd"));
    }

    [Fact]
    public async Task DownloadFileAsync_ValidFileId_ReturnsFileContent()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        DataModels.UploadedFile uploadedFile =new DataModels.UploadedFile
        {
            Id = fileId,
            Category = "quotations",
            EntityId = "QUO-001",
            OriginalFileName = "test.pdf",
            ObjectName = "business-documents/quotations/QUO-001/20241201_120000_test.pdf",
            ContentType = "application/pdf",
            FileSize = 1024,
            Bucket = "maliev-dev",
            UploadedAt = DateTime.UtcNow,
            UploadedBy = "test-user",
            AccessLevel = DataModels.AccessLevel.Internal
        };

        _context.UploadedFiles.Add(uploadedFile);
        await _context.SaveChangesAsync();

        var fileContent = Encoding.UTF8.GetBytes("PDF content");
        _mockStorageService
            .Setup(s => s.DownloadFileAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new FileDownloadResponse
            {
                Content = fileContent,
                ContentType = "application/pdf",
                FileName = "test.pdf"
            });

        // Act
        var result = await _fileService.DownloadFileAsync(fileId, "test-user", "127.0.0.1");

        // Assert
        result.Should().NotBeNull();
        result!.FileName.Should().Be("test.pdf");
        result.ContentType.Should().Be("application/pdf");

        // Verify access log was created
        var accessLog = await _context.FileAccessLogs.FirstOrDefaultAsync();
        accessLog.Should().NotBeNull();
        accessLog!.FileId.Should().Be(fileId);
        accessLog.AccessType.Should().Be(DataModels.AccessType.Download);
        accessLog.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DownloadFileAsync_NonExistentFile_ReturnsNull()
    {
        // Arrange
        var nonExistentFileId = Guid.NewGuid();

        // Act
        var result = await _fileService.DownloadFileAsync(nonExistentFileId, "test-user", "127.0.0.1");

        // Assert
        result.Should().BeNull();

        // Verify failed access log was created
        var accessLog = await _context.FileAccessLogs.FirstOrDefaultAsync();
        accessLog.Should().NotBeNull();
        accessLog!.IsSuccess.Should().BeFalse();
        accessLog.ErrorMessage.Should().Be("File not found");
    }

    [Fact]
    public async Task DeleteFileAsync_ValidFile_SoftDeletesSuccessfully()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        DataModels.UploadedFile uploadedFile =new DataModels.UploadedFile
        {
            Id = fileId,
            Category = "temp",
            EntityId = "TEMP-001",
            OriginalFileName = "temp.txt",
            ObjectName = "temp/TEMP-001/20241201_120000_temp.txt",
            ContentType = "text/plain",
            FileSize = 100,
            Bucket = "maliev-dev",
            UploadedAt = DateTime.UtcNow,
            UploadedBy = "test-user",
            AccessLevel = DataModels.AccessLevel.Internal
        };

        _context.UploadedFiles.Add(uploadedFile);
        await _context.SaveChangesAsync();

        _mockStorageService
            .Setup(s => s.DeleteFileAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act
        var result = await _fileService.DeleteFileAsync(fileId, "test-user", "127.0.0.1");

        // Assert
        result.Should().BeTrue();

        // Verify soft delete
        var deletedFile = await _context.UploadedFiles.FirstOrDefaultAsync(f => f.Id == fileId);
        deletedFile.Should().NotBeNull();
        deletedFile!.IsDeleted.Should().BeTrue();
        deletedFile.DeletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        deletedFile.DeletedBy.Should().Be("test-user");

        // Verify access log
        var accessLog = await _context.FileAccessLogs.FirstOrDefaultAsync(l => l.AccessType == DataModels.AccessType.Delete);
        accessLog.Should().NotBeNull();
        accessLog!.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetFilesAsync_WithFilters_ReturnsFilteredResults()
    {
        // Arrange
        var files = new[]
        {
            new DataModels.UploadedFile
            {
                Id = Guid.NewGuid(),
                Category = "quotations",
                EntityId = "QUO-001",
                CustomerId = "CUST-001",
                OriginalFileName = "quote1.pdf",
                ContentType = "application/pdf",
                FileSize = 1024,
                UploadedAt = DateTime.UtcNow.AddDays(-1),
                UploadedBy = "user1",
                AccessLevel = DataModels.AccessLevel.Internal
            },
            new DataModels.UploadedFile
            {
                Id = Guid.NewGuid(),
                Category = "invoices",
                EntityId = "INV-001",
                CustomerId = "CUST-001",
                OriginalFileName = "invoice1.pdf",
                ContentType = "application/pdf",
                FileSize = 2048,
                UploadedAt = DateTime.UtcNow,
                UploadedBy = "user2",
                AccessLevel = DataModels.AccessLevel.Internal
            }
        };

        _context.UploadedFiles.AddRange(files);
        await _context.SaveChangesAsync();

        var query = new FileListQueryRequest
        {
            Category = "quotations",
            CustomerId = "CUST-001",
            PageSize = 10,
            Page = 1
        };

        // Act
        var result = await _fileService.GetFilesAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
        result.Files.Should().HaveCount(1);
        result.Files.First().Category.Should().Be("quotations");
        result.Files.First().OriginalFileName.Should().Be("quote1.pdf");
    }

    [Fact]
    public async Task GenerateDownloadUrlAsync_ValidFile_ReturnsSignedUrl()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        DataModels.UploadedFile uploadedFile =new DataModels.UploadedFile
        {
            Id = fileId,
            Category = "quotations",
            EntityId = "QUO-001",
            OriginalFileName = "test.pdf",
            ObjectName = "business-documents/quotations/QUO-001/20241201_120000_test.pdf",
            Bucket = "maliev-dev",
            ContentType = "application/pdf",
            FileSize = 1024,
            UploadedAt = DateTime.UtcNow,
            UploadedBy = "test-user",
            AccessLevel = DataModels.AccessLevel.Internal
        };

        _context.UploadedFiles.Add(uploadedFile);
        await _context.SaveChangesAsync();

        var expectedUrl = "https://signed-url-example.com/download";
        _mockStorageService
            .Setup(s => s.GenerateSignedUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<bool>()))
            .ReturnsAsync(expectedUrl);

        // Act
        var result = await _fileService.GenerateDownloadUrlAsync(fileId, TimeSpan.FromHours(1));

        // Assert
        result.Should().Be(expectedUrl);
        _mockStorageService.Verify(s => s.GenerateSignedUrlAsync("maliev-dev", uploadedFile.ObjectName, TimeSpan.FromHours(1), false), Times.Once);
    }

    [Fact]
    public async Task GenerateDownloadUrlAsync_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentFileId = Guid.NewGuid();

        // Act & Assert
        await FluentActions.Invoking(() => _fileService.GenerateDownloadUrlAsync(nonExistentFileId))
            .Should().ThrowAsync<FileNotFoundException>()
            .WithMessage($"File {nonExistentFileId} not found");
    }

    private static Mock<IFormFile> CreateMockFile(string fileName, string contentType, byte[] content)
    {
        var mockFile = new Mock<IFormFile>();
        var stream = new MemoryStream(content);

        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.ContentType).Returns(contentType);
        mockFile.Setup(f => f.Length).Returns(content.Length);
        mockFile.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(content));
        mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), default))
            .Returns((Stream target, CancellationToken token) => stream.CopyToAsync(target, token));

        return mockFile;
    }

    public void Dispose()
    {
        _context.Dispose();
        _memoryCache.Dispose();
    }
}