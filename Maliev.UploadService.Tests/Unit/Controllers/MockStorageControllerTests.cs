using Maliev.UploadService.Api.Controllers.v1;
using Maliev.UploadService.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Maliev.UploadService.Tests.Unit.Controllers;

/// <summary>
/// Unit tests for <see cref="MockStorageController"/>.
/// </summary>
public sealed class MockStorageControllerTests
{
    [Fact]
    public async Task Download_WithValidSignedToken_ReturnsStoredFileContent()
    {
        var service = CreateService();
        var storagePath = $"intranet/uploads/{Guid.NewGuid():N}.step";
        var content = "mock file"u8.ToArray();
        await service.UploadFileAsync(new MemoryStream(content), storagePath, "model/step", overwrite: true);
        var signedUrl = await service.GenerateSignedUrlAsync(storagePath, TimeSpan.FromMinutes(5));
        var token = new Uri(signedUrl).Segments[^1];

        var controller = CreateController(HttpMethods.Get);

        var result = controller.Download(token);

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("model/step", fileResult.ContentType);
        Assert.Equal(content, fileResult.FileContents);
        Assert.Equal(content.LongLength, controller.Response.ContentLength);
    }

    [Fact]
    public void Download_WithUnknownToken_ReturnsNotFound()
    {
        var controller = CreateController(HttpMethods.Get);

        var result = controller.Download("missing-token");

        Assert.IsType<NotFoundResult>(result);
    }

    private static MockStorageController CreateController(string method)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;

        return new MockStorageController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
    }

    private static MockStorageService CreateService()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "http";
        httpContext.Request.Host = new HostString("localhost", 55333);

        return new MockStorageService(
            NullLogger<MockStorageService>.Instance,
            new HttpContextAccessor { HttpContext = httpContext },
            new ConfigurationBuilder().Build());
    }
}
