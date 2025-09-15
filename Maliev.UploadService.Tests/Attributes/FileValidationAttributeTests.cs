using FluentAssertions;
using Maliev.UploadService.Api.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Moq;
using System.Text;

namespace Maliev.UploadService.Tests.Attributes;

public class FileValidationAttributeTests
{
    private readonly Mock<HttpContext> _mockHttpContext;
    private readonly Mock<HttpRequest> _mockRequest;
    private readonly ActionExecutingContext _actionContext;
    private readonly FileValidationAttribute _attribute;

    public FileValidationAttributeTests()
    {
        _mockHttpContext = new Mock<HttpContext>();
        _mockRequest = new Mock<HttpRequest>();
        _attribute = new FileValidationAttribute();

        _mockHttpContext.Setup(c => c.Request).Returns(_mockRequest.Object);

        _actionContext = new ActionExecutingContext(
            new ActionContext(_mockHttpContext.Object, new(), new()),
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            Mock.Of<Controller>()
        );
    }

    [Fact]
    public void OnActionExecuting_NonMultipartRequest_ReturnsBadRequest()
    {
        // Arrange
        _mockRequest.Setup(r => r.HasFormContentType).Returns(false);

        // Act
        _attribute.OnActionExecuting(_actionContext);

        // Assert
        _actionContext.Result.Should().BeOfType<BadRequestObjectResult>();
        var result = (BadRequestObjectResult)_actionContext.Result;
        var error = result.Value;
        error.Should().BeEquivalentTo(new { error = "Request must be multipart/form-data" });
    }

    [Fact]
    public void OnActionExecuting_NoFiles_ReturnsBadRequest()
    {
        // Arrange
        var mockFormCollection = new Mock<IFormCollection>();
        var emptyFileCollection = new FormFileCollection();

        _mockRequest.Setup(r => r.HasFormContentType).Returns(true);
        _mockRequest.Setup(r => r.Form).Returns(mockFormCollection.Object);
        mockFormCollection.Setup(f => f.Files).Returns(emptyFileCollection);

        // Act
        _attribute.OnActionExecuting(_actionContext);

        // Assert
        _actionContext.Result.Should().BeOfType<BadRequestObjectResult>();
        var result = (BadRequestObjectResult)_actionContext.Result;
        var error = result.Value;
        error.Should().BeEquivalentTo(new { error = "No files provided" });
    }

    [Fact]
    public void OnActionExecuting_EmptyFile_ReturnsBadRequest()
    {
        // Arrange
        var mockFile = CreateMockFile("empty.txt", "text/plain", Array.Empty<byte>());
        var fileCollection = new FormFileCollection { mockFile.Object };
        var mockFormCollection = new Mock<IFormCollection>();

        _mockRequest.Setup(r => r.HasFormContentType).Returns(true);
        _mockRequest.Setup(r => r.Form).Returns(mockFormCollection.Object);
        mockFormCollection.Setup(f => f.Files).Returns(fileCollection);

        // Act
        _attribute.OnActionExecuting(_actionContext);

        // Assert
        _actionContext.Result.Should().BeOfType<BadRequestObjectResult>();
        var result = (BadRequestObjectResult)_actionContext.Result;
        var error = result.Value;
        error.Should().BeEquivalentTo(new { error = "File is empty", fileName = "empty.txt" });
    }

    [Fact]
    public void OnActionExecuting_FileTooLarge_ReturnsBadRequest()
    {
        // Arrange
        var largeFile = new byte[101 * 1024 * 1024]; // 101MB
        var mockFile = CreateMockFile("large.pdf", "application/pdf", largeFile);
        var fileCollection = new FormFileCollection { mockFile.Object };
        var mockFormCollection = new Mock<IFormCollection>();

        _mockRequest.Setup(r => r.HasFormContentType).Returns(true);
        _mockRequest.Setup(r => r.Form).Returns(mockFormCollection.Object);
        mockFormCollection.Setup(f => f.Files).Returns(fileCollection);

        // Act
        _attribute.OnActionExecuting(_actionContext);

        // Assert
        _actionContext.Result.Should().BeOfType<BadRequestObjectResult>();
        var result = (BadRequestObjectResult)_actionContext.Result;
        result.Value.Should().NotBeNull();

        // Use reflection to get error details from anonymous object
        var errorResponse = result.Value!;
        var errorType = errorResponse.GetType();
        var errorProperty = errorType.GetProperty("error");
        var fileNameProperty = errorType.GetProperty("fileName");

        errorProperty!.GetValue(errorResponse)!.ToString().Should().Contain("File size exceeds maximum allowed size");
        fileNameProperty!.GetValue(errorResponse)!.ToString().Should().Be("large.pdf");
    }

    [Theory]
    [InlineData("malware.exe")]
    [InlineData("script.bat")]
    [InlineData("command.cmd")]
    [InlineData("virus.scr")]
    [InlineData("hack.js")]
    public void OnActionExecuting_DangerousFileExtensions_ReturnsBadRequest(string fileName)
    {
        // Arrange
        var fileContent = Encoding.UTF8.GetBytes("harmless content");
        var mockFile = CreateMockFile(fileName, "application/octet-stream", fileContent);
        var fileCollection = new FormFileCollection { mockFile.Object };
        var mockFormCollection = new Mock<IFormCollection>();

        _mockRequest.Setup(r => r.HasFormContentType).Returns(true);
        _mockRequest.Setup(r => r.Form).Returns(mockFormCollection.Object);
        mockFormCollection.Setup(f => f.Files).Returns(fileCollection);

        // Act
        _attribute.OnActionExecuting(_actionContext);

        // Assert
        _actionContext.Result.Should().BeOfType<BadRequestObjectResult>();
        var result = (BadRequestObjectResult)_actionContext.Result;
        result.Value.Should().NotBeNull();

        // Use reflection to get error details from anonymous object
        var errorResponse = result.Value!;
        var errorType = errorResponse.GetType();
        var errorProperty = errorType.GetProperty("error");
        var fileNameProperty = errorType.GetProperty("fileName");

        var extension = Path.GetExtension(fileName);
        errorProperty!.GetValue(errorResponse)!.ToString().Should().Contain($"File type '{extension}' is not allowed for security reasons");
        fileNameProperty!.GetValue(errorResponse)!.ToString().Should().Be(fileName);
    }

    [Fact]
    public void OnActionExecuting_MaliciousContent_PE_ReturnsBadRequest()
    {
        // Arrange - PE executable signature
        var maliciousContent = new byte[] { 0x4D, 0x5A, 0x90, 0x00 }; // PE signature
        var mockFile = CreateMockFile("document.pdf", "application/pdf", maliciousContent);
        var fileCollection = new FormFileCollection { mockFile.Object };
        var mockFormCollection = new Mock<IFormCollection>();

        _mockRequest.Setup(r => r.HasFormContentType).Returns(true);
        _mockRequest.Setup(r => r.Form).Returns(mockFormCollection.Object);
        mockFormCollection.Setup(f => f.Files).Returns(fileCollection);

        // Act
        _attribute.OnActionExecuting(_actionContext);

        // Assert
        _actionContext.Result.Should().BeOfType<BadRequestObjectResult>();
        var result = (BadRequestObjectResult)_actionContext.Result;
        result.Value.Should().NotBeNull();

        // Use reflection to get error details from anonymous object
        var errorResponse = result.Value!;
        var errorType = errorResponse.GetType();
        var errorProperty = errorType.GetProperty("error");
        var fileNameProperty = errorType.GetProperty("fileName");

        errorProperty!.GetValue(errorResponse)!.ToString().Should().Be("File contains potentially malicious content");
        fileNameProperty!.GetValue(errorResponse)!.ToString().Should().Be("document.pdf");
    }

    [Fact]
    public void OnActionExecuting_MaliciousContent_Script_ReturnsBadRequest()
    {
        // Arrange - Script content in PDF
        var maliciousContent = Encoding.UTF8.GetBytes("<script>alert('xss')</script>");
        var mockFile = CreateMockFile("document.pdf", "application/pdf", maliciousContent);
        var fileCollection = new FormFileCollection { mockFile.Object };
        var mockFormCollection = new Mock<IFormCollection>();

        _mockRequest.Setup(r => r.HasFormContentType).Returns(true);
        _mockRequest.Setup(r => r.Form).Returns(mockFormCollection.Object);
        mockFormCollection.Setup(f => f.Files).Returns(fileCollection);

        // Act
        _attribute.OnActionExecuting(_actionContext);

        // Assert
        _actionContext.Result.Should().BeOfType<BadRequestObjectResult>();
        var result = (BadRequestObjectResult)_actionContext.Result;
        result.Value.Should().NotBeNull();

        // Use reflection to get error details from anonymous object
        var errorResponse = result.Value!;
        var errorType = errorResponse.GetType();
        var errorProperty = errorType.GetProperty("error");

        errorProperty!.GetValue(errorResponse)!.ToString().Should().Be("File contains potentially malicious content");
    }

    [Fact]
    public void OnActionExecuting_ValidFile_ContinuesExecution()
    {
        // Arrange
        var validContent = Encoding.UTF8.GetBytes("This is a valid document content");
        var mockFile = CreateMockFile("document.pdf", "application/pdf", validContent);
        var fileCollection = new FormFileCollection { mockFile.Object };
        var mockFormCollection = new Mock<IFormCollection>();

        _mockRequest.Setup(r => r.HasFormContentType).Returns(true);
        _mockRequest.Setup(r => r.Form).Returns(mockFormCollection.Object);
        mockFormCollection.Setup(f => f.Files).Returns(fileCollection);

        // Act
        _attribute.OnActionExecuting(_actionContext);

        // Assert
        _actionContext.Result.Should().BeNull(); // Should continue to action
    }

    [Fact]
    public void OnActionExecuting_AllowedContentTypes_ValidatesCorrectly()
    {
        // Arrange
        var attributeWithContentTypes = new FileValidationAttribute(
            allowedContentTypes: new[] { "application/pdf", "image/jpeg" });

        var validContent = Encoding.UTF8.GetBytes("PDF content");
        var mockFile = CreateMockFile("document.pdf", "application/pdf", validContent);
        var fileCollection = new FormFileCollection { mockFile.Object };
        var mockFormCollection = new Mock<IFormCollection>();

        _mockRequest.Setup(r => r.HasFormContentType).Returns(true);
        _mockRequest.Setup(r => r.Form).Returns(mockFormCollection.Object);
        mockFormCollection.Setup(f => f.Files).Returns(fileCollection);

        // Act
        attributeWithContentTypes.OnActionExecuting(_actionContext);

        // Assert
        _actionContext.Result.Should().BeNull(); // Should continue to action
    }

    [Fact]
    public void OnActionExecuting_DisallowedContentType_ReturnsBadRequest()
    {
        // Arrange
        var attributeWithContentTypes = new FileValidationAttribute(
            allowedContentTypes: new[] { "application/pdf" });

        var validContent = Encoding.UTF8.GetBytes("Image content");
        var mockFile = CreateMockFile("image.jpg", "image/jpeg", validContent);
        var fileCollection = new FormFileCollection { mockFile.Object };
        var mockFormCollection = new Mock<IFormCollection>();

        _mockRequest.Setup(r => r.HasFormContentType).Returns(true);
        _mockRequest.Setup(r => r.Form).Returns(mockFormCollection.Object);
        mockFormCollection.Setup(f => f.Files).Returns(fileCollection);

        // Act
        attributeWithContentTypes.OnActionExecuting(_actionContext);

        // Assert
        _actionContext.Result.Should().BeOfType<BadRequestObjectResult>();
        var result = (BadRequestObjectResult)_actionContext.Result;
        result.Value.Should().NotBeNull();

        // Use reflection to get error details from anonymous object
        var errorResponse = result.Value!;
        var errorType = errorResponse.GetType();
        var errorProperty = errorType.GetProperty("error");
        var contentTypeProperty = errorType.GetProperty("contentType");

        errorProperty!.GetValue(errorResponse)!.ToString().Should().Be("File content type is not allowed");
        contentTypeProperty!.GetValue(errorResponse)!.ToString().Should().Be("image/jpeg");
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
}