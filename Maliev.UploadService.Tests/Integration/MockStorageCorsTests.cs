using System.Net;
using Maliev.UploadService.Api.Services;
using Maliev.UploadService.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Maliev.UploadService.Tests.Integration;

[Collection("Database")]
public sealed class MockStorageCorsTests
{
    private const string IntranetOrigin = "http://localhost:5071";

    private readonly TestWebApplicationFactory _factory;

    public MockStorageCorsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Download_WithIntranetOrigin_ReturnsCorsHeader()
    {
        var content = "glb-bytes"u8.ToArray();
        var token = await CreateSignedObjectAsync(content);
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/upload/v1/mock-storage/{token}");
        request.Headers.TryAddWithoutValidation("Origin", IntranetOrigin);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(content, await response.Content.ReadAsByteArrayAsync());
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins));
        Assert.Contains("*", origins);
    }

    [Fact]
    public async Task DownloadPreflight_WithIntranetOrigin_AllowsGet()
    {
        var token = await CreateSignedObjectAsync("glb-bytes"u8.ToArray());
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, $"/upload/v1/mock-storage/{token}");
        request.Headers.TryAddWithoutValidation("Origin", IntranetOrigin);
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins));
        Assert.Contains("*", origins);
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Methods", out var methods));
        Assert.Contains(methods, value => value.Contains("GET", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<string> CreateSignedObjectAsync(byte[] content)
    {
        var service = CreateService();
        var storagePath = $"intranet/viewer/{Guid.NewGuid():N}.glb";
        await service.UploadFileAsync(
            new MemoryStream(content),
            storagePath,
            "model/gltf-binary",
            overwrite: true);

        var signedUrl = await service.GenerateSignedUrlAsync(storagePath, TimeSpan.FromMinutes(5));
        return new Uri(signedUrl).Segments[^1];
    }

    private static MockStorageService CreateService()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "http";
        httpContext.Request.Host = new HostString("localhost", 55333);

        return new MockStorageService(
            NullLogger<MockStorageService>.Instance,
            new HttpContextAccessor { HttpContext = httpContext });
    }
}
