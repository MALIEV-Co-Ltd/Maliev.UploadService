using Maliev.Aspire.ServiceDefaults.IAM;
using Maliev.Aspire.ServiceDefaults.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Maliev.UploadService.Tests.Unit.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    private readonly Mock<ILogger<ExceptionHandlingMiddleware>> _mockLogger;
    private readonly Mock<IHostEnvironment> _mockEnvironment;
    private readonly ExceptionHandlingMiddleware _middleware;

    public ExceptionHandlingMiddlewareTests()
    {
        _mockLogger = new Mock<ILogger<ExceptionHandlingMiddleware>>();
        _mockEnvironment = new Mock<IHostEnvironment>();
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns("Testing");
        _middleware = new ExceptionHandlingMiddleware(
            next: (HttpContext context) => Task.CompletedTask,
            logger: _mockLogger.Object,
            environment: _mockEnvironment.Object
        );
    }

    [Fact]
    public async Task InvokeAsync_NoException_CallsNext()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var nextCalled = false;
        var middleware = new ExceptionHandlingMiddleware(
            next: (HttpContext ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            logger: _mockLogger.Object,
            environment: _mockEnvironment.Object
        );

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_ArgumentException_ReturnsBadRequest()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(
            next: (HttpContext ctx) => throw new ArgumentException("Invalid argument"),
            logger: _mockLogger.Object,
            environment: _mockEnvironment.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var response = JsonSerializer.Deserialize<JsonElement>(responseBody);

        Assert.Equal("Invalid argument", response.GetProperty("error").GetString());
        Assert.Equal(400, response.GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task InvokeAsync_UnauthorizedAccessException_ReturnsForbidden()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(
            next: (HttpContext ctx) => throw new UnauthorizedAccessException("Access denied"),
            logger: _mockLogger.Object,
            environment: _mockEnvironment.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert - UnauthorizedAccessException returns 401 Unauthorized, not 403 Forbidden
        Assert.Equal((int)HttpStatusCode.Unauthorized, context.Response.StatusCode);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var response = JsonSerializer.Deserialize<JsonElement>(responseBody);

        Assert.Equal("Unauthorized access", response.GetProperty("error").GetString());
        Assert.Equal(401, response.GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task InvokeAsync_KeyNotFoundException_ReturnsNotFound()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(
            next: (HttpContext ctx) => throw new KeyNotFoundException("Resource not found"),
            logger: _mockLogger.Object,
            environment: _mockEnvironment.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal((int)HttpStatusCode.NotFound, context.Response.StatusCode);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var response = JsonSerializer.Deserialize<JsonElement>(responseBody);

        Assert.Equal("Resource not found", response.GetProperty("error").GetString());
        Assert.Equal(404, response.GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task InvokeAsync_InvalidOperationException_ReturnsConflict()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(
            next: (HttpContext ctx) => throw new InvalidOperationException("Operation not allowed"),
            logger: _mockLogger.Object,
            environment: _mockEnvironment.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert - InvalidOperationException returns 400 BadRequest, not 409 Conflict
        Assert.Equal((int)HttpStatusCode.BadRequest, context.Response.StatusCode);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var response = JsonSerializer.Deserialize<JsonElement>(responseBody);

        Assert.Equal("Operation not allowed", response.GetProperty("error").GetString());
        Assert.Equal(400, response.GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task InvokeAsync_GenericException_ReturnsInternalServerError()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(
            next: (HttpContext ctx) => throw new Exception("Unexpected error"),
            logger: _mockLogger.Object,
            environment: _mockEnvironment.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var response = JsonSerializer.Deserialize<JsonElement>(responseBody);

        // For internal server errors, the message should be generic
        Assert.Equal("An internal server error occurred", response.GetProperty("error").GetString());
        Assert.Equal(500, response.GetProperty("statusCode").GetInt32());
    }

    [Fact]
    public async Task InvokeAsync_Exception_LogsError()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var expectedException = new Exception("Test exception");
        var middleware = new ExceptionHandlingMiddleware(
            next: (HttpContext ctx) => throw expectedException,
            logger: _mockLogger.Object,
            environment: _mockEnvironment.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("An unhandled exception occurred")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ResponseFormat_IsCamelCase()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(
            next: (HttpContext ctx) => throw new ArgumentException("Test"),
            logger: _mockLogger.Object,
            environment: _mockEnvironment.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();

        // Verify camelCase naming (error, statusCode, details, traceId)
        Assert.Contains("\"error\"", responseBody);
        Assert.Contains("\"statusCode\"", responseBody);
        Assert.Contains("\"traceId\"", responseBody);
    }
}


