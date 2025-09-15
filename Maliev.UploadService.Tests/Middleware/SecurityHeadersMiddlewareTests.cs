using FluentAssertions;
using Maliev.UploadService.Api.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Maliev.UploadService.Tests.Middleware;

public class SecurityHeadersMiddlewareTests
{
    private readonly Mock<ILogger<SecurityHeadersMiddleware>> _mockLogger;
    private readonly Mock<RequestDelegate> _mockNext;
    private readonly SecurityHeadersMiddleware _middleware;
    private readonly DefaultHttpContext _httpContext;

    public SecurityHeadersMiddlewareTests()
    {
        _mockLogger = new Mock<ILogger<SecurityHeadersMiddleware>>();
        _mockNext = new Mock<RequestDelegate>();
        _middleware = new SecurityHeadersMiddleware(_mockNext.Object, _mockLogger.Object);
        _httpContext = new DefaultHttpContext();
    }

    [Fact]
    public async Task InvokeAsync_ShouldAddAllSecurityHeaders()
    {
        // Arrange
        _httpContext.Request.Scheme = "https";
        _httpContext.Request.IsHttps = true;

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        var headers = _httpContext.Response.Headers;

        // Check Content Security Policy
        headers.Should().ContainKey("Content-Security-Policy");
        headers["Content-Security-Policy"].ToString().Should().Contain("default-src 'self'");
        headers["Content-Security-Policy"].ToString().Should().Contain("frame-ancestors 'none'");

        // Check X-Frame-Options
        headers.Should().ContainKey("X-Frame-Options");
        headers["X-Frame-Options"].ToString().Should().Be("DENY");

        // Check X-Content-Type-Options
        headers.Should().ContainKey("X-Content-Type-Options");
        headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");

        // Check X-XSS-Protection
        headers.Should().ContainKey("X-XSS-Protection");
        headers["X-XSS-Protection"].ToString().Should().Be("1; mode=block");

        // Check Referrer-Policy
        headers.Should().ContainKey("Referrer-Policy");
        headers["Referrer-Policy"].ToString().Should().Be("strict-origin-when-cross-origin");

        // Check Permissions-Policy
        headers.Should().ContainKey("Permissions-Policy");
        headers["Permissions-Policy"].ToString().Should().Contain("camera=()");
        headers["Permissions-Policy"].ToString().Should().Contain("microphone=()");
    }

    [Fact]
    public async Task InvokeAsync_WithHttpsRequest_ShouldAddHSTSHeader()
    {
        // Arrange
        _httpContext.Request.Scheme = "https";
        _httpContext.Request.IsHttps = true;

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        var headers = _httpContext.Response.Headers;
        headers.Should().ContainKey("Strict-Transport-Security");
        headers["Strict-Transport-Security"].ToString().Should().Be("max-age=31536000; includeSubDomains; preload");
    }

    [Fact]
    public async Task InvokeAsync_WithHttpRequest_ShouldNotAddHSTSHeader()
    {
        // Arrange
        _httpContext.Request.Scheme = "http";
        _httpContext.Request.IsHttps = false;

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        var headers = _httpContext.Response.Headers;
        headers.Should().NotContainKey("Strict-Transport-Security");
    }

    [Fact]
    public async Task InvokeAsync_ShouldRemoveServerInformationHeaders()
    {
        // Arrange
        _httpContext.Response.Headers["Server"] = "Microsoft-IIS/10.0";
        _httpContext.Response.Headers["X-Powered-By"] = "ASP.NET";
        _httpContext.Response.Headers["X-AspNet-Version"] = "4.0.30319";
        _httpContext.Response.Headers["X-AspNetMvc-Version"] = "5.2";

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        var headers = _httpContext.Response.Headers;
        headers.Should().NotContainKey("Server");
        headers.Should().NotContainKey("X-Powered-By");
        headers.Should().NotContainKey("X-AspNet-Version");
        headers.Should().NotContainKey("X-AspNetMvc-Version");
    }

    [Fact]
    public async Task InvokeAsync_ShouldCallNextMiddleware()
    {
        // Arrange
        var nextCalled = false;
        _mockNext.Setup(x => x(_httpContext))
            .Returns(Task.CompletedTask)
            .Callback(() => nextCalled = true);

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        nextCalled.Should().BeTrue();
        _mockNext.Verify(x => x(_httpContext), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithExistingSecurityHeaders_ShouldNotDuplicate()
    {
        // Arrange
        _httpContext.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
        _httpContext.Response.Headers["X-Content-Type-Options"] = "nosniff";

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        var headers = _httpContext.Response.Headers;

        // Should have only one value (the original one should be replaced)
        headers["X-Frame-Options"].Count.Should().Be(1);
        headers["X-Content-Type-Options"].Count.Should().Be(1);

        // Values should be updated to the middleware's values
        headers["X-Frame-Options"].ToString().Should().Be("DENY");
        headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
    }

    [Fact]
    public async Task InvokeAsync_ShouldSetContentSecurityPolicyWithCorrectDirectives()
    {
        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        var cspHeader = _httpContext.Response.Headers["Content-Security-Policy"].ToString();

        cspHeader.Should().Contain("default-src 'self'");
        cspHeader.Should().Contain("script-src 'self' 'unsafe-inline' 'unsafe-eval'");
        cspHeader.Should().Contain("style-src 'self' 'unsafe-inline'");
        cspHeader.Should().Contain("img-src 'self' data: https:");
        cspHeader.Should().Contain("font-src 'self' https:");
        cspHeader.Should().Contain("connect-src 'self'");
        cspHeader.Should().Contain("frame-ancestors 'none'");
    }

    [Fact]
    public async Task InvokeAsync_ShouldSetPermissionsPolicyWithCorrectDirectives()
    {
        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        var permissionsHeader = _httpContext.Response.Headers["Permissions-Policy"].ToString();

        permissionsHeader.Should().Contain("camera=()");
        permissionsHeader.Should().Contain("microphone=()");
        permissionsHeader.Should().Contain("geolocation=()");
        permissionsHeader.Should().Contain("payment=()");
        permissionsHeader.Should().Contain("usb=()");
    }

    [Theory]
    [InlineData("https", true)]
    [InlineData("http", false)]
    public async Task InvokeAsync_WithDifferentSchemes_ShouldHandleHSTSCorrectly(string scheme, bool isHttps)
    {
        // Arrange
        _httpContext.Request.Scheme = scheme;
        _httpContext.Request.IsHttps = isHttps;

        // Act
        await _middleware.InvokeAsync(_httpContext);

        // Assert
        var headers = _httpContext.Response.Headers;

        if (isHttps)
        {
            headers.Should().ContainKey("Strict-Transport-Security");
            headers["Strict-Transport-Security"].ToString().Should().Contain("max-age=31536000");
            headers["Strict-Transport-Security"].ToString().Should().Contain("includeSubDomains");
            headers["Strict-Transport-Security"].ToString().Should().Contain("preload");
        }
        else
        {
            headers.Should().NotContainKey("Strict-Transport-Security");
        }
    }

    [Fact]
    public async Task InvokeAsync_WhenNextThrowsException_ShouldStillAddHeaders()
    {
        // Arrange
        _mockNext.Setup(x => x(_httpContext))
            .ThrowsAsync(new InvalidOperationException("Next middleware failed"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _middleware.InvokeAsync(_httpContext));

        exception.Message.Should().Be("Next middleware failed");

        // Verify headers were still added before the exception
        var headers = _httpContext.Response.Headers;
        headers.Should().ContainKey("X-Frame-Options");
        headers.Should().ContainKey("X-Content-Type-Options");
        headers.Should().ContainKey("X-XSS-Protection");
    }

    [Fact]
    public void SecurityHeadersMiddlewareExtensions_UseSecurityHeaders_ShouldReturnBuilder()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockBuilder = new Mock<IApplicationBuilder>();
        mockBuilder.Setup(x => x.ApplicationServices).Returns(services.BuildServiceProvider());

        // Act & Assert - Just verify the extension method doesn't throw
        var act = () => mockBuilder.Object.UseSecurityHeaders();
        act.Should().NotThrow();
    }
}