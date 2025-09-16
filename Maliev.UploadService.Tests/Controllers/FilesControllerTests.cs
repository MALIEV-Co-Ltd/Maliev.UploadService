using FluentAssertions;
using Maliev.UploadService.Api.Controllers;
using Maliev.UploadService.Api.Models;
using Maliev.UploadService.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using System.Text;

namespace Maliev.UploadService.Tests.Controllers;

public class FilesControllerTests
{
    private readonly Mock<IFileStorageService> _mockFileStorageService;
    private readonly Mock<ILogger<FilesController>> _mockLogger;
    private readonly FilesController _controller;

    public FilesControllerTests()
    {
        _mockFileStorageService = new Mock<IFileStorageService>();
        _mockLogger = new Mock<ILogger<FilesController>>();
        _controller = new FilesController(_mockFileStorageService.Object, _mockLogger.Object);

        // Setup controller context
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Setup user context
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "test-user"),
            new(ClaimTypes.Name, "test-user"),
            new(ClaimTypes.Role, "Sales")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext.HttpContext.User = principal;

        // Setup connection info
        _controller.ControllerContext.HttpContext.Connection.RemoteIpAddress =
            System.Net.IPAddress.Parse("127.0.0.1");
    }

    [Fact]
    public async Task UploadFile_ValidRequest_ReturnsCreated()
    {
        // Arrange
        var mockFile = CreateMockFile("test.pdf", "application/pdf", "PDF content");
        var request = new FileUploadRequest
        {
            File = mockFile.Object,
            ObjectPath = "quotations/QUO-001/documents/test.pdf",
            AccessLevel = AccessLevel.Internal
        };

        var expectedResponse = new FileUploadResponse
        {
            FileId = Guid.NewGuid(),
            ObjectName = "quotations/QUO-001/documents/test.pdf",
            Bucket = "maliev-dev",
            FileSize = 11,
            ContentType = "application/pdf",
            UploadedAt = DateTime.UtcNow,
            AccessLevel = AccessLevel.Internal,
            ProcessingStatus = ProcessingStatus.Completed
        };

        _mockFileStorageService
            .Setup(s => s.UploadFileToPathAsync(
                It.IsAny<string>(),
                It.IsAny<IFormFile>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<StorageOptions>(),
                It.IsAny<Dictionary<string, string>>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.UploadFile(request);

        // Assert
        result.Result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = (CreatedAtActionResult)result.Result!;
        createdResult.StatusCode.Should().Be(201);
        createdResult.Value.Should().BeEquivalentTo(expectedResponse);

        _mockFileStorageService.Verify(s => s.UploadFileToPathAsync(
            "quotations/QUO-001/documents/test.pdf",
            It.IsAny<IFormFile>(),
            "test-user",
            "127.0.0.1",
            It.IsAny<StorageOptions>(),
            It.IsAny<Dictionary<string, string>>()), Times.Once);
    }

    [Fact]
    public async Task UploadFile_ServiceThrowsArgumentException_ReturnsBadRequest()
    {
        // Arrange
        var mockFile = CreateMockFile("test.pdf", "application/pdf", "PDF content");
        var request = new FileUploadRequest
        {
            File = mockFile.Object,
            ObjectPath = "invalid/path",
            AccessLevel = AccessLevel.Internal
        };

        _mockFileStorageService
            .Setup(s => s.UploadFileToPathAsync(
                It.IsAny<string>(),
                It.IsAny<IFormFile>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<StorageOptions>(),
                It.IsAny<Dictionary<string, string>>()))
            .ThrowsAsync(new ArgumentException("Invalid path"));

        // Act
        var result = await _controller.UploadFile(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)result.Result!;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task UploadFileToPath_ValidRequest_ReturnsCreated()
    {
        // Arrange
        var mockFile = CreateMockFile("test.pdf", "application/pdf", "PDF content");
        var objectPath = "quotations/QUO-001/documents/test.pdf";

        var expectedResponse = new FileUploadResponse
        {
            FileId = Guid.NewGuid(),
            ObjectName = objectPath,
            Bucket = "maliev-dev",
            FileSize = 11,
            ContentType = "application/pdf",
            UploadedAt = DateTime.UtcNow,
            AccessLevel = AccessLevel.Internal,
            ProcessingStatus = ProcessingStatus.Completed
        };

        _mockFileStorageService
            .Setup(s => s.UploadFileToPathAsync(
                It.IsAny<string>(),
                It.IsAny<IFormFile>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<StorageOptions>(),
                It.IsAny<Dictionary<string, string>>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.UploadFileToPath(objectPath, mockFile.Object);

        // Assert
        result.Result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = (CreatedAtActionResult)result.Result!;
        createdResult.StatusCode.Should().Be(201);
        createdResult.Value.Should().BeEquivalentTo(expectedResponse);
    }

    [Fact]
    public async Task UploadFile_EmptyObjectPath_ReturnsBadRequest()
    {
        // Arrange
        var mockFile = CreateMockFile("test.pdf", "application/pdf", "PDF content");
        var request = new FileUploadRequest
        {
            File = mockFile.Object,
            ObjectPath = "",
            AccessLevel = AccessLevel.Internal
        };

        // Act
        var result = await _controller.UploadFile(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)result.Result!;
        badRequestResult.StatusCode.Should().Be(400);
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
}