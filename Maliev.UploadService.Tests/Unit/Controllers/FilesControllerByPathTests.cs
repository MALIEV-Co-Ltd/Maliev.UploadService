using MassTransit;
using Maliev.UploadService.Api.Controllers.v1;
using Maliev.UploadService.Api.Models.Requests;
using Maliev.UploadService.Api.Services;
using Maliev.UploadService.Api.Services.Auth;
using Maliev.UploadService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;

namespace Maliev.UploadService.Tests.Unit.Controllers;

/// <summary>
/// Unit tests for <see cref="FilesController.GenerateSignedUrlByPath"/>.
/// Verifies the GCS existence check added to prevent handing out 404-destined signed URLs.
/// </summary>
public class FilesControllerByPathTests
{
    private static FilesController MakeController(
        bool fileExists,
        bool canAccess = true,
        string? cachedUrl = null,
        string signedUrl = "https://storage.googleapis.com/signed?sig=mock")
    {
        var storageMock = new Mock<IStorageService>();
        storageMock
            .Setup(s => s.FileExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileExists);
        storageMock
            .Setup(s => s.GenerateSignedUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(signedUrl);

        var authMock = new Mock<IAuthorizationPolicyService>();
        authMock
            .Setup(a => a.CanAccessPathAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(canAccess);

        var cacheOptions = Options.Create(new MemoryDistributedCacheOptions());
        IDistributedCache cache;
        if (cachedUrl != null)
        {
            var realCache = new MemoryDistributedCache(cacheOptions);
            var cacheKey = $"signed-url:path:some/path.stl:60";
            realCache.SetString(cacheKey, $"{cachedUrl}|{DateTime.UtcNow.AddMinutes(60):O}");
            cache = realCache;
        }
        else
        {
            cache = new MemoryDistributedCache(cacheOptions);
        }

        var dbOptions = new DbContextOptionsBuilder<UploadDbContext>()
            .UseNpgsql("Host=fake;Database=fake")
            .Options;
        var dbContext = new UploadDbContext(dbOptions);

        var publishMock = new Mock<IPublishEndpoint>();

        var controller = new FilesController(
            storageMock.Object,
            authMock.Object,
            dbContext,
            cache,
            NullLogger<FilesController>.Instance,
            publishMock.Object);

        // Set up a fake user so [RequirePermission] can read the identity name
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "test-service")],
            "Bearer"));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        return controller;
    }

    [Fact]
    public async Task GenerateSignedUrlByPath_Returns410_WhenFileNotFoundInGcs()
    {
        var controller = MakeController(fileExists: false);
        var request = new GenerateSignedUrlByPathRequest { StoragePath = "some/path.stl", ExpirationMinutes = 60 };

        var result = await controller.GenerateSignedUrlByPath(request, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status410Gone, objectResult.StatusCode);
    }

    [Fact]
    public async Task GenerateSignedUrlByPath_Returns200_WhenFileExistsInGcs()
    {
        var controller = MakeController(fileExists: true);
        var request = new GenerateSignedUrlByPathRequest { StoragePath = "some/path.stl", ExpirationMinutes = 60 };

        var result = await controller.GenerateSignedUrlByPath(request, CancellationToken.None);

        var objectResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
    }

    [Fact]
    public async Task GenerateSignedUrlByPath_Returns403_WhenPathUnauthorized()
    {
        var controller = MakeController(fileExists: true, canAccess: false);
        var request = new GenerateSignedUrlByPathRequest { StoragePath = "some/path.stl", ExpirationMinutes = 60 };

        var result = await controller.GenerateSignedUrlByPath(request, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task GenerateSignedUrlByPath_ReturnsCached_AfterAuthorization_WithoutExistenceCheck_WhenCacheHit()
    {
        var storageMock = new Mock<IStorageService>(MockBehavior.Strict);
        // FileExistsAsync should NOT be called when cache is warm
        storageMock
            .Setup(s => s.FileExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // would return 410 if called

        var authMock = new Mock<IAuthorizationPolicyService>();
        authMock.Setup(a => a.CanAccessPathAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var cacheOptions = Options.Create(new MemoryDistributedCacheOptions());
        var cache = new MemoryDistributedCache(cacheOptions);
        var cacheKey = $"signed-url:path:some/path.stl:60";
        var cachedSignedUrl = "https://storage.googleapis.com/cached?sig=abc";
        cache.SetString(cacheKey, $"{cachedSignedUrl}|{DateTime.UtcNow.AddMinutes(60):O}");

        var dbOptions = new DbContextOptionsBuilder<UploadDbContext>()
            .UseNpgsql("Host=fake;Database=fake")
            .Options;

        var controller = new FilesController(
            storageMock.Object,
            authMock.Object,
            new UploadDbContext(dbOptions),
            cache,
            NullLogger<FilesController>.Instance,
            new Mock<IPublishEndpoint>().Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.Name, "test-service")], "Bearer"))
            }
        };

        var request = new GenerateSignedUrlByPathRequest { StoragePath = "some/path.stl", ExpirationMinutes = 60 };
        var result = await controller.GenerateSignedUrlByPath(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        authMock.Verify(
            a => a.CanAccessPathAsync("test-service", "some/path.stl", It.IsAny<CancellationToken>()),
            Times.Once);
        // Should NOT have called FileExistsAsync because cache hit happens after authorization
        storageMock.Verify(
            s => s.FileExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
