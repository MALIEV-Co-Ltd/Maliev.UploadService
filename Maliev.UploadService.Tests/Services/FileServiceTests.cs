using FluentAssertions;
using Maliev.UploadService.Api.Models;
using Maliev.UploadService.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text;

namespace Maliev.UploadService.Tests.Services;

public class FileServiceTests : IDisposable
{
    private readonly Mock<IGoogleCloudStorageService> _mockStorageService;
    private readonly Mock<ILogger<FileStorageService>> _mockLogger;
    private readonly FileStorageService _fileService;

    public FileServiceTests()
    {
        // Create mocks
        _mockStorageService = new Mock<IGoogleCloudStorageService>();
        _mockLogger = new Mock<ILogger<FileStorageService>>();

        var mockOptions = new Mock<IOptions<StorageServiceOptions>>();
        mockOptions.Setup(x => x.Value).Returns(new StorageServiceOptions
        {
            DefaultBucketName = "test-bucket"
        });

        _fileService = new FileStorageService(
            _mockStorageService.Object,
            mockOptions.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task UploadFileToPathAsync_ValidFile_ReturnsSuccess()
    {
        // Arrange
        var objectPath = "quotations/QUO-001/documents/test.pdf";
        var mockFile = CreateMockFile("test.pdf", "application/pdf", "PDF content");
        var uploadedBy = "test-user";
        var ipAddress = "127.0.0.1";

        var expectedResponse = new FileUploadResponse
        {
            FileId = Guid.NewGuid(),
            ObjectName = objectPath,
            Bucket = "test-bucket",
            FileSize = 11,
            ContentType = "application/pdf",
            UploadedAt = DateTime.UtcNow,
            AccessLevel = AccessLevel.Internal,
            ProcessingStatus = ProcessingStatus.Completed
        };

        _mockStorageService
            .Setup(s => s.UploadFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<string>()))
            .ReturnsAsync("test-object-name");

        // Act
        var result = await _fileService.UploadFileToPathAsync(
            objectPath, mockFile.Object, uploadedBy, ipAddress);

        // Assert
        result.Should().NotBeNull();
        result.ObjectName.Should().Be(objectPath);
        result.ContentType.Should().Be("application/pdf");
        result.FileSize.Should().Be(11);

        _mockStorageService.Verify(s => s.UploadFileAsync(
            "test-bucket",
            objectPath,
            It.IsAny<Stream>(),
            "application/pdf"), Times.Once);
    }

    [Fact]
    public async Task UploadFileToPathAsync_EmptyObjectPath_ThrowsArgumentException()
    {
        // Arrange
        var mockFile = CreateMockFile("test.pdf", "application/pdf", "PDF content");
        var uploadedBy = "test-user";
        var ipAddress = "127.0.0.1";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _fileService.UploadFileToPathAsync("", mockFile.Object, uploadedBy, ipAddress));
    }

    [Fact]
    public async Task DownloadFileByPathAsync_ValidPath_ReturnsFile()
    {
        // Arrange
        var objectPath = "quotations/QUO-001/documents/test.pdf";
        var accessedBy = "test-user";
        var ipAddress = "127.0.0.1";

        var mockResponse = new FileDownloadResponse
        {
            Content = Encoding.UTF8.GetBytes("PDF content"),
            ContentType = "application/pdf",
            FileSize = 11,
            FileName = "test.pdf"
        };

        _mockStorageService
            .Setup(s => s.DownloadFileAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await _fileService.DownloadFileByPathAsync(objectPath, accessedBy, ipAddress);

        // Assert
        result.Should().NotBeNull();
        result.ContentType.Should().Be("application/pdf");
        result.FileSize.Should().Be(11);

        _mockStorageService.Verify(s => s.DownloadFileAsync("test-bucket", objectPath), Times.Once);
    }

    [Fact]
    public async Task DeleteFileByPathAsync_ValidPath_ReturnsTrue()
    {
        // Arrange
        var objectPath = "quotations/QUO-001/documents/test.pdf";
        var deletedBy = "test-user";
        var ipAddress = "127.0.0.1";

        _mockStorageService
            .Setup(s => s.DeleteFileAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act
        var result = await _fileService.DeleteFileByPathAsync(objectPath, deletedBy, ipAddress);

        // Assert
        result.Should().BeTrue();

        _mockStorageService.Verify(s => s.DeleteFileAsync("test-bucket", objectPath), Times.Once);
    }

    [Fact]
    public async Task FileExistsByPathAsync_ExistingFile_ReturnsTrue()
    {
        // Arrange
        var objectPath = "quotations/QUO-001/documents/test.pdf";

        _mockStorageService
            .Setup(s => s.FileExistsAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act
        var result = await _fileService.FileExistsByPathAsync(objectPath);

        // Assert
        result.Should().BeTrue();

        _mockStorageService.Verify(s => s.FileExistsAsync("test-bucket", objectPath), Times.Once);
    }

    [Fact]
    public async Task GenerateSignedUrlByPathAsync_ValidPath_ReturnsUrl()
    {
        // Arrange
        var objectPath = "quotations/QUO-001/documents/test.pdf";
        var expectedUrl = "https://signed-url.example.com";

        _mockStorageService
            .Setup(s => s.GenerateSignedUrlAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<bool>()))
            .ReturnsAsync(expectedUrl);

        // Act
        var result = await _fileService.GenerateSignedUrlByPathAsync(objectPath);

        // Assert
        result.Should().Be(expectedUrl);

        _mockStorageService.Verify(s => s.GenerateSignedUrlAsync(
            "test-bucket",
            objectPath,
            It.IsAny<TimeSpan>(),
            false), Times.Once);
    }

    private Mock<IFormFile> CreateMockFile(string fileName, string contentType, string content)
    {
        var mockFile = new Mock<IFormFile>();
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.ContentType).Returns(contentType);
        mockFile.Setup(f => f.Length).Returns(stream.Length);
        mockFile.Setup(f => f.OpenReadStream()).Returns(stream);
        mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
               .Returns((Stream target, CancellationToken token) => stream.CopyToAsync(target, token));

        return mockFile;
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}