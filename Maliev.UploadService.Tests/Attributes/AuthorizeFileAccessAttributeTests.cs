using FluentAssertions;
using Maliev.UploadService.Api.Attributes;
using Maliev.UploadService.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Security.Claims;

namespace Maliev.UploadService.Tests.Attributes;

public class AuthorizeFileAccessAttributeTests
{
    private readonly Mock<IAuthorizationService> _mockAuthService;
    private readonly Mock<HttpContext> _mockHttpContext;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly AuthorizeFileAccessAttribute _attribute;
    private readonly ActionExecutingContext _actionContext;

    public AuthorizeFileAccessAttributeTests()
    {
        _mockAuthService = new Mock<IAuthorizationService>();
        _mockHttpContext = new Mock<HttpContext>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _attribute = new AuthorizeFileAccessAttribute();

        _mockHttpContext.Setup(c => c.RequestServices).Returns(_mockServiceProvider.Object);
        _mockServiceProvider.Setup(s => s.GetService(typeof(IAuthorizationService)))
            .Returns(_mockAuthService.Object);

        _actionContext = new ActionExecutingContext(
            new ActionContext(_mockHttpContext.Object, new(), new()),
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            Mock.Of<Controller>()
        );
    }

    [Fact]
    public async Task OnActionExecutionAsync_NoAuthorizationService_Returns500()
    {
        // Arrange
        _mockServiceProvider.Setup(s => s.GetService(typeof(IAuthorizationService)))
            .Returns((IAuthorizationService?)null);

        // Act
        await _attribute.OnActionExecutionAsync(_actionContext, MockNext());

        // Assert
        _actionContext.Result.Should().BeOfType<StatusCodeResult>();
        var result = (StatusCodeResult)_actionContext.Result;
        result.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task OnActionExecutionAsync_MissingFileId_ReturnsBadRequest()
    {
        // Arrange
        var user = CreateUserWithClaims("user1", new[] { "Sales" });
        _mockHttpContext.Setup(c => c.User).Returns(user);

        // No fileId in action arguments

        // Act
        await _attribute.OnActionExecutionAsync(_actionContext, MockNext());

        // Assert
        _actionContext.Result.Should().BeOfType<BadRequestObjectResult>();
        var result = (BadRequestObjectResult)_actionContext.Result;
        result.Value.Should().NotBeNull();

        // Use reflection to get error details from anonymous object
        var errorResponse = result.Value!;
        var errorType = errorResponse.GetType();
        var errorProperty = errorType.GetProperty("error");

        errorProperty!.GetValue(errorResponse)!.ToString().Should().Contain("Invalid or missing fileId");
    }

    [Fact]
    public async Task OnActionExecutionAsync_InvalidFileId_ReturnsBadRequest()
    {
        // Arrange
        var user = CreateUserWithClaims("user1", new[] { "Sales" });
        _mockHttpContext.Setup(c => c.User).Returns(user);

        _actionContext.ActionArguments["fileId"] = "invalid-guid";

        // Act
        await _attribute.OnActionExecutionAsync(_actionContext, MockNext());

        // Assert
        _actionContext.Result.Should().BeOfType<BadRequestObjectResult>();
        var result = (BadRequestObjectResult)_actionContext.Result;
        result.Value.Should().NotBeNull();

        // Use reflection to get error details from anonymous object
        var errorResponse = result.Value!;
        var errorType = errorResponse.GetType();
        var errorProperty = errorType.GetProperty("error");

        errorProperty!.GetValue(errorResponse)!.ToString().Should().Contain("Invalid or missing fileId");
    }

    [Fact]
    public async Task OnActionExecutionAsync_ReadAccess_Authorized_ContinuesExecution()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var user = CreateUserWithClaims("user1", new[] { "Sales" });
        var attribute = new AuthorizeFileAccessAttribute("fileId", "read");

        _mockHttpContext.Setup(c => c.User).Returns(user);
        _actionContext.ActionArguments["fileId"] = fileId;

        _mockAuthService.Setup(a => a.CanAccessFileAsync(fileId, "user1", new[] { "Sales" }))
            .ReturnsAsync(true);

        var nextCalled = false;
        ActionExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(
                new ActionContext(_mockHttpContext.Object, new(), new()),
                new List<IFilterMetadata>(),
                Mock.Of<Controller>())
            {
                Result = new OkResult()
            });
        };

        // Act
        await attribute.OnActionExecutionAsync(_actionContext, next);

        // Assert
        _actionContext.Result.Should().BeNull();
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task OnActionExecutionAsync_ReadAccess_NotAuthorized_ReturnsForbid()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var user = CreateUserWithClaims("user1", new[] { "Guest" });
        var attribute = new AuthorizeFileAccessAttribute("fileId", "read");

        _mockHttpContext.Setup(c => c.User).Returns(user);
        _actionContext.ActionArguments["fileId"] = fileId;

        _mockAuthService.Setup(a => a.CanAccessFileAsync(fileId, "user1", new[] { "Guest" }))
            .ReturnsAsync(false);

        // Act
        await attribute.OnActionExecutionAsync(_actionContext, MockNext());

        // Assert
        _actionContext.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task OnActionExecutionAsync_DeleteAccess_Authorized_ContinuesExecution()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var user = CreateUserWithClaims("user1", new[] { "Manager" });
        var attribute = new AuthorizeFileAccessAttribute("fileId", "delete");

        _mockHttpContext.Setup(c => c.User).Returns(user);
        _actionContext.ActionArguments["fileId"] = fileId;

        _mockAuthService.Setup(a => a.CanDeleteFileAsync(fileId, "user1", new[] { "Manager" }))
            .ReturnsAsync(true);

        var nextCalled = false;
        ActionExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(
                new ActionContext(_mockHttpContext.Object, new(), new()),
                new List<IFilterMetadata>(),
                Mock.Of<Controller>())
            {
                Result = new OkResult()
            });
        };

        // Act
        await attribute.OnActionExecutionAsync(_actionContext, next);

        // Assert
        _actionContext.Result.Should().BeNull();
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task OnActionExecutionAsync_DeleteAccess_NotAuthorized_ReturnsForbid()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var user = CreateUserWithClaims("user1", new[] { "Guest" });
        var attribute = new AuthorizeFileAccessAttribute("fileId", "delete");

        _mockHttpContext.Setup(c => c.User).Returns(user);
        _actionContext.ActionArguments["fileId"] = fileId;

        _mockAuthService.Setup(a => a.CanDeleteFileAsync(fileId, "user1", new[] { "Guest" }))
            .ReturnsAsync(false);

        // Act
        await attribute.OnActionExecutionAsync(_actionContext, MockNext());

        // Assert
        _actionContext.Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task OnActionExecutionAsync_AnonymousUser_UsesCorrectUserId()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var anonymousUser = new ClaimsPrincipal(new ClaimsIdentity()); // No claims

        _mockHttpContext.Setup(c => c.User).Returns(anonymousUser);
        _actionContext.ActionArguments["fileId"] = fileId;

        _mockAuthService.Setup(a => a.CanAccessFileAsync(fileId, "anonymous", Array.Empty<string>()))
            .ReturnsAsync(false);

        // Act
        await _attribute.OnActionExecutionAsync(_actionContext, MockNext());

        // Assert
        _mockAuthService.Verify(a => a.CanAccessFileAsync(fileId, "anonymous", Array.Empty<string>()), Times.Once);
        _actionContext.Result.Should().BeOfType<ForbidResult>();
    }

    [Theory]
    [InlineData("customFileId")]
    [InlineData("documentId")]
    [InlineData("id")]
    public async Task OnActionExecutionAsync_CustomFileIdParameter_ExtractsCorrectly(string paramName)
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var user = CreateUserWithClaims("user1", new[] { "Sales" });
        var attribute = new AuthorizeFileAccessAttribute(paramName, "read");

        _mockHttpContext.Setup(c => c.User).Returns(user);
        _actionContext.ActionArguments[paramName] = fileId;

        _mockAuthService.Setup(a => a.CanAccessFileAsync(fileId, "user1", new[] { "Sales" }))
            .ReturnsAsync(true);

        // Act
        await attribute.OnActionExecutionAsync(_actionContext, MockNext());

        // Assert
        _mockAuthService.Verify(a => a.CanAccessFileAsync(fileId, "user1", new[] { "Sales" }), Times.Once);
    }

    [Theory]
    [InlineData("download")]
    [InlineData("unknown")]
    public async Task OnActionExecutionAsync_UnknownAction_DefaultsToReadAccess(string action)
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var user = CreateUserWithClaims("user1", new[] { "Sales" });
        var attribute = new AuthorizeFileAccessAttribute("fileId", action);

        _mockHttpContext.Setup(c => c.User).Returns(user);
        _actionContext.ActionArguments["fileId"] = fileId;

        _mockAuthService.Setup(a => a.CanAccessFileAsync(fileId, "user1", new[] { "Sales" }))
            .ReturnsAsync(true);

        // Act
        await attribute.OnActionExecutionAsync(_actionContext, MockNext());

        // Assert
        _mockAuthService.Verify(a => a.CanAccessFileAsync(fileId, "user1", new[] { "Sales" }), Times.Once);
        _mockAuthService.Verify(a => a.CanDeleteFileAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string[]>()), Times.Never);
    }

    private static ClaimsPrincipal CreateUserWithClaims(string userId, string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, userId)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }

    private ActionExecutionDelegate MockNext()
    {
        return () => Task.FromResult(new ActionExecutedContext(
            new ActionContext(_mockHttpContext.Object, new(), new()),
            new List<IFilterMetadata>(),
            Mock.Of<Controller>())
        {
            Result = new OkResult()
        });
    }
}