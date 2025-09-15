using FluentAssertions;
using Maliev.UploadService.Api.Controllers;
using Maliev.UploadService.Api.Models;
using DataModels = Maliev.UploadService.Data.Models;
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
            Category = "quotations",
            EntityId = "QUO-001",
            CustomerId = "CUST-001",
            AccessLevel = AccessLevel.Internal,
            Tags = new[] { "test" }
        };

        var expectedResponse = new FileUploadResponse
        {
            FileId = Guid.NewGuid(),
            ObjectName = "business-documents/quotations/QUO-001/20241201_120000_test.pdf",
            Bucket = "maliev-dev",
            FileSize = 11,
            ContentType = "application/pdf",
            UploadedAt = DateTime.UtcNow,
            Category = "quotations",
            EntityId = "QUO-001",
            AccessLevel = AccessLevel.Internal,
            ProcessingStatus = ProcessingStatus.Completed
        };

        _mockFileService
            .Setup(s => s.UploadFileAsync(It.IsAny<FileUploadRequest>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.UploadFile(request);

        // Assert
        result.Result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = (CreatedAtActionResult)result.Result!;
        createdResult.StatusCode.Should().Be(201);
        createdResult.Value.Should().BeEquivalentTo(expectedResponse);

        _mockFileService.Verify(s => s.UploadFileAsync(
            It.Is<FileUploadRequest>(r => r.Category == "quotations" && r.EntityId == "QUO-001"),
            "test-user",
            "127.0.0.1"), Times.Once);
    }

    [Fact]
    public async Task UploadFile_ServiceThrowsArgumentException_ReturnsBadRequest()
    {
        // Arrange
        var mockFile = CreateMockFile("test.exe", "application/octet-stream", "exe content");
        var request = new FileUploadRequest
        {
            File = mockFile.Object,
            Category = "temp",
            EntityId = "TEMP-001"
        };

        _mockFileService
            .Setup(s => s.UploadFileAsync(It.IsAny<FileUploadRequest>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new ArgumentException("File type '.exe' is not allowed for security reasons"));

        // Act
        var result = await _controller.UploadFile(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = (BadRequestObjectResult)result.Result!;
        badRequestResult.StatusCode.Should().Be(400);

        var problemDetails = badRequestResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Title.Should().Be("Invalid Request");
        problemDetails.Detail.Should().Contain("File type '.exe' is not allowed");
    }

    [Fact]
    public async Task UploadFile_ServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var mockFile = CreateMockFile("test.pdf", "application/pdf", "PDF content");
        var request = new FileUploadRequest
        {
            File = mockFile.Object,
            Category = "quotations",
            EntityId = "QUO-001"
        };

        _mockFileService
            .Setup(s => s.UploadFileAsync(It.IsAny<FileUploadRequest>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Storage service unavailable"));

        // Act
        var result = await _controller.UploadFile(request);

        // Assert
        result.Result.Should().BeOfType<ObjectResult>();
        var errorResult = (ObjectResult)result.Result!;
        errorResult.StatusCode.Should().Be(500);

        var problemDetails = errorResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Title.Should().Be("Upload Failed");
        problemDetails.Detail.Should().Contain("An unexpected error occurred during file upload");
    }

    [Fact]
    public async Task GetFileMetadata_ValidFileId_ReturnsOk()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var expectedMetadata = new FileMetadataResponse
        {
            FileId = fileId,
            OriginalFileName = "test.pdf",
            ObjectName = "business-documents/quotations/QUO-001/20241201_120000_test.pdf",
            ContentType = "application/pdf",
            FileSize = 1024,
            Bucket = "maliev-dev",
            UploadedAt = DateTime.UtcNow,
            UploadedBy = "test-user",
            Category = "quotations",
            EntityId = "QUO-001",
            AccessLevel = AccessLevel.Internal,
            ProcessingStatus = ProcessingStatus.Completed
        };

        _mockFileService
            .Setup(s => s.GetFileMetadataAsync(fileId))
            .ReturnsAsync(expectedMetadata);

        // Act
        var result = await _controller.GetFileMetadata(fileId);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result.Result!;
        okResult.Value.Should().BeEquivalentTo(expectedMetadata);
    }

    [Fact]
    public async Task GetFileMetadata_NonExistentFile_ReturnsNotFound()
    {
        // Arrange
        var fileId = Guid.NewGuid();

        _mockFileService
            .Setup(s => s.GetFileMetadataAsync(fileId))
            .ReturnsAsync((FileMetadataResponse?)null);

        // Act
        var result = await _controller.GetFileMetadata(fileId);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = (NotFoundObjectResult)result.Result!;

        var problemDetails = notFoundResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Title.Should().Be("File Not Found");
        problemDetails.Detail.Should().Contain(fileId.ToString());
    }

    [Fact]
    public async Task DownloadFile_ValidFileId_ReturnsFile()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var fileContent = Encoding.UTF8.GetBytes("PDF file content");
        var downloadResponse = new FileDownloadResponse
        {
            Content = fileContent,
            FileName = "test.pdf",
            ContentType = "application/pdf"
        };

        _mockFileService
            .Setup(s => s.DownloadFileAsync(fileId, It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(downloadResponse);

        // Act
        var result = await _controller.DownloadFile(fileId);

        // Assert
        result.Should().BeOfType<FileContentResult>();
        var fileResult = (FileContentResult)result;
        fileResult.FileDownloadName.Should().Be("test.pdf");
        fileResult.ContentType.Should().Be("application/pdf");

        _mockFileService.Verify(s => s.DownloadFileAsync(fileId, "test-user", "127.0.0.1"), Times.Once);
    }

    [Fact]
    public async Task DownloadFile_NonExistentFile_ReturnsNotFound()
    {
        // Arrange
        var fileId = Guid.NewGuid();

        _mockFileService
            .Setup(s => s.DownloadFileAsync(fileId, It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((FileDownloadResponse?)null);

        // Act
        var result = await _controller.DownloadFile(fileId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = (NotFoundObjectResult)result;

        var problemDetails = notFoundResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Title.Should().Be("File Not Found");
        problemDetails.Detail.Should().Contain(fileId.ToString());
    }

    [Fact]
    public async Task DeleteFile_ValidFileId_ReturnsNoContent()
    {
        // Arrange
        var fileId = Guid.NewGuid();

        _mockFileService
            .Setup(s => s.DeleteFileAsync(fileId, It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteFile(fileId);

        // Assert
        result.Should().BeOfType<NoContentResult>();

        _mockFileService.Verify(s => s.DeleteFileAsync(fileId, "test-user", "127.0.0.1"), Times.Once);
    }

    [Fact]
    public async Task DeleteFile_NonExistentFile_ReturnsNotFound()
    {
        // Arrange
        var fileId = Guid.NewGuid();

        _mockFileService
            .Setup(s => s.DeleteFileAsync(fileId, It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteFile(fileId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = (NotFoundObjectResult)result;

        var problemDetails = notFoundResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Title.Should().Be("File Not Found");
        problemDetails.Detail.Should().Contain(fileId.ToString());
    }

    [Fact]
    public async Task GetFiles_ValidQuery_ReturnsOk()
    {
        // Arrange
        var query = new FileListQueryRequest
        {
            Category = "quotations",
            CustomerId = "CUST-001",
            PageSize = 10,
            Page = 1
        };

        var expectedResponse = new FileListResponse
        {
            Files = new List<FileMetadataResponse>
            {
                new()
                {
                    FileId = Guid.NewGuid(),
                    OriginalFileName = "test.pdf",
                    Category = "quotations",
                    EntityId = "QUO-001",
                    ContentType = "application/pdf",
                    FileSize = 1024,
                    UploadedAt = DateTime.UtcNow,
                    AccessLevel = AccessLevel.Internal
                }
            },
            TotalCount = 1,
            Page = 1,
            PageSize = 10,
            TotalPages = 1,
            HasNextPage = false,
            HasPreviousPage = false
        };

        _mockFileService
            .Setup(s => s.GetFilesAsync(It.IsAny<FileListQueryRequest>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.GetFiles(query);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result.Result!;
        okResult.Value.Should().BeEquivalentTo(expectedResponse);
    }

    [Fact]
    public async Task GenerateSignedUrl_ValidFileId_ReturnsOk()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var expectedUrl = "https://signed-url-example.com/download";

        _mockFileService
            .Setup(s => s.GenerateDownloadUrlAsync(fileId, It.IsAny<TimeSpan?>()))
            .ReturnsAsync(expectedUrl);

        // Act
        var result = await _controller.GenerateSignedUrl(fileId, 1);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result.Result!;
        var response = okResult.Value.Should().BeOfType<SignedUrlResponse>().Subject;
        response.SignedUrl.Should().Be(expectedUrl);
        response.FileId.Should().Be(fileId);
    }

    [Fact]
    public async Task GenerateSignedUrl_NonExistentFile_ReturnsNotFound()
    {
        // Arrange
        var fileId = Guid.NewGuid();

        _mockFileService
            .Setup(s => s.GenerateDownloadUrlAsync(fileId, It.IsAny<TimeSpan?>()))
            .ThrowsAsync(new FileNotFoundException($"File {fileId} not found"));

        // Act
        var result = await _controller.GenerateSignedUrl(fileId, 1);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = (NotFoundObjectResult)result.Result!;

        var problemDetails = notFoundResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Title.Should().Be("File Not Found");
    }

    private static Mock<IFormFile> CreateMockFile(string fileName, string contentType, string content)
    {
        var mockFile = new Mock<IFormFile>();
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);

        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.ContentType).Returns(contentType);
        mockFile.Setup(f => f.Length).Returns(bytes.Length);
        mockFile.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(bytes));
        mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), default))
            .Returns((Stream target, CancellationToken token) => stream.CopyToAsync(target, token));

        return mockFile;
    }
}