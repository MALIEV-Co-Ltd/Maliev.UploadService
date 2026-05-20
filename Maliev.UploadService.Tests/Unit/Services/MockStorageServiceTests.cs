using Maliev.UploadService.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Maliev.UploadService.Tests.Unit.Services;

/// <summary>
/// Tests for the local development mock storage service.
/// </summary>
public sealed class MockStorageServiceTests
{
    [Fact]
    public async Task GenerateSignedUrlAsync_AfterUpload_ReturnsDownloadableMockUrl()
    {
        var service = CreateService();
        var storagePath = $"intranet/uploads/{Guid.NewGuid():N}.step";
        var content = "mock step content"u8.ToArray();

        await service.UploadFileAsync(
            new MemoryStream(content),
            storagePath,
            "model/step",
            overwrite: true);

        var signedUrl = await service.GenerateSignedUrlAsync(storagePath, TimeSpan.FromMinutes(5));
        var token = new Uri(signedUrl).Segments[^1];

        Assert.StartsWith("http://localhost:55333/upload/v1/mock-storage/", signedUrl, StringComparison.Ordinal);
        Assert.True(MockStorageService.TryGetSignedObject(token, out var storedContent, out var contentType, out var signedStoragePath));
        Assert.Equal(storagePath, signedStoragePath);
        Assert.Equal("model/step", contentType);
        Assert.Equal(content, storedContent);
    }

    [Fact]
    public async Task ResumeUploadAsync_WhenUploadCompletes_StoresChunkBytesForSignedUrl()
    {
        var service = CreateService();
        var storagePath = $"intranet/resumable/{Guid.NewGuid():N}.stl";
        var content = "solid test"u8.ToArray();

        var session = await service.InitiateResumableUploadAsync(
            storagePath,
            "model/stl",
            content.Length);

        var progress = await service.ResumeUploadAsync(
            session.SessionUri,
            new MemoryStream(content),
            startByte: 0,
            endByte: content.Length - 1,
            totalSize: content.Length);

        Assert.True(progress.IsComplete);

        var signedUrl = await service.GenerateSignedUrlAsync(storagePath, TimeSpan.FromMinutes(5));
        var token = new Uri(signedUrl).Segments[^1];

        Assert.True(MockStorageService.TryGetSignedObject(token, out var storedContent, out var contentType, out var signedStoragePath));
        Assert.Equal(storagePath, signedStoragePath);
        Assert.Equal("model/stl", contentType);
        Assert.Equal(content, storedContent);
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
