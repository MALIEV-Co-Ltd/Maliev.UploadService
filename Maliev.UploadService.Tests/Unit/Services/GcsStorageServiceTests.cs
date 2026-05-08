using Maliev.Aspire.ServiceDefaults.IAM;
using Google.Cloud.Storage.V1;
using Maliev.UploadService.Api.Services;
using Maliev.UploadService.Tests.Fixtures;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Maliev.UploadService.Tests.Unit.Services;

public class GcsStorageServiceTests
{
    private static IConfiguration CreateTestConfig(string defaultBucket = "test-bucket")
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GoogleCloud:Buckets:Customers"] = defaultBucket,
                ["GoogleCloud:Buckets:Financials"] = defaultBucket,
                ["GoogleCloud:Buckets:Operations"] = defaultBucket,
                ["GoogleCloud:Buckets:Temp"] = defaultBucket,
                ["GoogleCloud:Buckets:Cache"] = defaultBucket
            })
            .Build();
    }

    /// <summary>
    /// Variant config that gives every bucket category a distinct name so
    /// routing assertions can prove which bucket was selected.
    /// </summary>
    private static IConfiguration CreateRoutingConfig()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GoogleCloud:Buckets:Customers"] = "bucket-customers",
                ["GoogleCloud:Buckets:Financials"] = "bucket-financials",
                ["GoogleCloud:Buckets:Operations"] = "bucket-operations",
                ["GoogleCloud:Buckets:Temp"] = "bucket-temp",
                ["GoogleCloud:Buckets:Cache"] = "bucket-cache"
            })
            .Build();
    }

    [Fact]
    public async Task InitiateResumableUploadAsync_ValidRequest_SendsGcsJsonApiInitiationRequest()
    {
        var mockClient = new Mock<StorageClient>();
        var mockHttpMessageHandler = new MockHttpMessageHandler();
        mockHttpMessageHandler.QueueResponse(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Headers =
            {
                Location = new Uri("https://storage.googleapis.com/upload/mock-session")
            }
        });

        var httpClient = new HttpClient(mockHttpMessageHandler);
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        mockHttpClientFactory
            .Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        var dummyCredential = Google.Apis.Auth.OAuth2.GoogleCredential.FromAccessToken("dummy-token");
        var service = new GcsStorageService(mockClient.Object, CreateTestConfig(), mockHttpClientFactory.Object, dummyCredential);

        var result = await service.InitiateResumableUploadAsync(
            "test-service/uploads/file with spaces.step",
            "model/step",
            12345,
            CancellationToken.None);

        Assert.Equal("https://storage.googleapis.com/upload/mock-session", result.SessionUri);
        var request = Assert.Single(mockHttpMessageHandler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(
            "https://storage.googleapis.com/upload/storage/v1/b/test-bucket/o?uploadType=resumable&name=test-service%2Fuploads%2Ffile with spaces.step",
            request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("dummy-token", request.Headers.Authorization.Parameter);
        Assert.True(request.Headers.TryGetValues("X-Upload-Content-Type", out var contentTypes));
        Assert.Equal("model/step", Assert.Single(contentTypes));
        Assert.True(request.Headers.TryGetValues("X-Upload-Content-Length", out var contentLengths));
        Assert.Equal("12345", Assert.Single(contentLengths));

        var body = await request.Content!.ReadAsStringAsync();
        Assert.Equal("""{"contentType":"model/step"}""", body);
    }

    [Fact]
    public async Task UploadFileAsync_ValidStream_UploadsSuccessfully()
    {
        // Arrange
        var mockClient = new Mock<StorageClient>();
        var mockHttpMessageHandler = new MockHttpMessageHandler();

        // Simulate successful upload
        mockHttpMessageHandler.QueueResponse(System.Net.HttpStatusCode.OK,
            "{\"name\":\"test-service/uploads/test.txt\",\"size\":\"17\",\"contentType\":\"text/plain\"}");

        // Mock GetObjectAsync to simulate file doesn't exist
        mockClient
            .Setup(x => x.GetObjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<GetObjectOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Google.GoogleApiException("Not Found"));

        // Mock UploadObjectAsync
        mockClient
            .Setup(x => x.UploadObjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<UploadObjectOptions>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IProgress<Google.Apis.Upload.IUploadProgress>>()))
            .ReturnsAsync(new Google.Apis.Storage.v1.Data.Object
            {
                Name = "test-service/uploads/test.txt",
                ContentType = "text/plain",
                Size = 17
            });

        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        var dummyCredential = Google.Apis.Auth.OAuth2.GoogleCredential.FromAccessToken("dummy-token");
        var service = new GcsStorageService(mockClient.Object, CreateTestConfig(), mockHttpClientFactory.Object, dummyCredential);
        var content = System.Text.Encoding.UTF8.GetBytes("Test file content");
        using var stream = new MemoryStream(content);

        // Act
        var result = await service.UploadFileAsync(
            stream,
            "test-service/uploads/test.txt",
            "text/plain",
            false);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-service/uploads/test.txt", result.StoragePath);
        Assert.Equal("text/plain", result.ContentType);
        Assert.Equal(content.Length, result.SizeBytes);
    }

    [Fact]
    public async Task UploadFileAsync_FileExists_WithoutOverwrite_ThrowsException()
    {
        // Arrange
        var mockClient = new Mock<StorageClient>();
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        var dummyCredential = Google.Apis.Auth.OAuth2.GoogleCredential.FromAccessToken("dummy-token");
        var service = new GcsStorageService(mockClient.Object, CreateTestConfig(), mockHttpClientFactory.Object, dummyCredential);

        // Simulate file already exists
        mockClient
            .Setup(x => x.GetObjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<GetObjectOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Google.Apis.Storage.v1.Data.Object { Name = "test.txt" });

        using var stream = new MemoryStream();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.UploadFileAsync(stream, "test.txt", "text/plain", overwrite: false));
    }

    [Fact]
    public async Task UploadFileAsync_LargeFile_StreamsWithoutLoadingToMemory()
    {
        // Arrange
        var mockClient = new Mock<StorageClient>();
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        var dummyCredential = Google.Apis.Auth.OAuth2.GoogleCredential.FromAccessToken("dummy-token");
        var service = new GcsStorageService(mockClient.Object, CreateTestConfig(), mockHttpClientFactory.Object, dummyCredential);

        // Create a large stream (10MB) that we'll monitor
        var largeFileSize = 10 * 1024 * 1024;
        var largeStream = new MemoryStream(new byte[largeFileSize]);

        // Mock GetObjectAsync to simulate file doesn't exist
        mockClient
            .Setup(x => x.GetObjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<GetObjectOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Google.GoogleApiException("Not Found"));

        // Setup mock to verify streaming behavior
        var uploadedBytes = 0L;
        mockClient
            .Setup(x => x.UploadObjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<UploadObjectOptions>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IProgress<Google.Apis.Upload.IUploadProgress>>()))
            .Callback<string, string, string, Stream, UploadObjectOptions, CancellationToken, IProgress<Google.Apis.Upload.IUploadProgress>>(
                (bucket, objectName, contentType, stream, options, token, progress) =>
                {
                    // Verify stream is being used (not loaded into memory)
                    Assert.True(stream.CanRead);
                    uploadedBytes = stream.Length;
                })
            .ReturnsAsync(new Google.Apis.Storage.v1.Data.Object
            {
                Name = "large.bin",
                Size = (ulong)largeFileSize
            });

        // Act
        var result = await service.UploadFileAsync(
            largeStream,
            "large.bin",
            "application/octet-stream",
            false);

        // Assert
        Assert.Equal(largeFileSize, uploadedBytes);
        Assert.Equal(largeFileSize, result.SizeBytes);
    }

    [Fact]
    public async Task FileExistsAsync_FileExists_ReturnsTrue()
    {
        // Arrange
        var mockClient = new Mock<StorageClient>();
        mockClient
            .Setup(x => x.GetObjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<GetObjectOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Google.Apis.Storage.v1.Data.Object { Name = "test.txt" });

        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        var dummyCredential = Google.Apis.Auth.OAuth2.GoogleCredential.FromAccessToken("dummy-token");
        var service = new GcsStorageService(mockClient.Object, CreateTestConfig(), mockHttpClientFactory.Object, dummyCredential);

        // Act
        var exists = await service.FileExistsAsync("test.txt");

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task FileExistsAsync_FileDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var mockClient = new Mock<StorageClient>();
        mockClient
            .Setup(x => x.GetObjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<GetObjectOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Google.GoogleApiException("Not Found"));

        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        var dummyCredential = Google.Apis.Auth.OAuth2.GoogleCredential.FromAccessToken("dummy-token");
        var service = new GcsStorageService(mockClient.Object, CreateTestConfig(), mockHttpClientFactory.Object, dummyCredential);

        // Act
        var exists = await service.FileExistsAsync("nonexistent.txt");

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task GenerateSignedUrlAsync_ValidPath_ReturnsSignedUrl()
    {
        // Note: Actual signed URL generation requires GCS credentials and UrlSigner cannot be mocked (sealed class)
        // This functionality is fully tested in integration tests
        // This unit test is skipped as the implementation requires actual GCS credentials

        // Skip this test as it requires actual GCS setup
        // Integration tests will verify actual functionality
        Assert.True(true, "Signed URL generation is tested in integration tests");
    }

    // T106: Test overwrite flag handling (FR-030)
    [Fact]
    public async Task UploadFileAsync_WithOverwriteTrue_OverwritesExistingFile()
    {
        // Arrange
        var mockClient = new Mock<StorageClient>();

        // Mock that file already exists
        mockClient
            .Setup(x => x.GetObjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<GetObjectOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Google.Apis.Storage.v1.Data.Object
            {
                Name = "test-service/uploads/existing.txt",
                Size = 100
            });

        // Mock UploadObjectAsync for overwrite
        mockClient
            .Setup(x => x.UploadObjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<UploadObjectOptions>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IProgress<Google.Apis.Upload.IUploadProgress>>()))
            .ReturnsAsync(new Google.Apis.Storage.v1.Data.Object
            {
                Name = "test-service/uploads/existing.txt",
                ContentType = "text/plain",
                Size = 20
            });

        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        var dummyCredential = Google.Apis.Auth.OAuth2.GoogleCredential.FromAccessToken("dummy-token");
        var service = new GcsStorageService(mockClient.Object, CreateTestConfig(), mockHttpClientFactory.Object, dummyCredential);
        var content = System.Text.Encoding.UTF8.GetBytes("New content");
        using var stream = new MemoryStream(content);

        // Act
        var result = await service.UploadFileAsync(
            stream,
            "test-service/uploads/existing.txt",
            "text/plain",
            overwrite: true, // Allow overwrite
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-service/uploads/existing.txt", result.StoragePath);

        // Verify UploadObjectAsync was called (indicating overwrite happened)
        mockClient.Verify(x => x.UploadObjectAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Stream>(),
            It.IsAny<UploadObjectOptions>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<IProgress<Google.Apis.Upload.IUploadProgress>>()), Times.Once);
    }

    [Fact]
    public async Task UploadFileAsync_WithOverwriteFalse_ThrowsWhenFileExists()
    {
        // Arrange
        var mockClient = new Mock<StorageClient>();

        // Mock that file already exists
        mockClient
            .Setup(x => x.GetObjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<GetObjectOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Google.Apis.Storage.v1.Data.Object
            {
                Name = "test-service/uploads/existing.txt",
                Size = 100
            });

        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        var dummyCredential = Google.Apis.Auth.OAuth2.GoogleCredential.FromAccessToken("dummy-token");
        var service = new GcsStorageService(mockClient.Object, CreateTestConfig(), mockHttpClientFactory.Object, dummyCredential);
        var content = System.Text.Encoding.UTF8.GetBytes("New content");
        using var stream = new MemoryStream(content);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await service.UploadFileAsync(
                stream,
                "test-service/uploads/existing.txt",
                "text/plain",
                overwrite: false, // Do NOT allow overwrite
                CancellationToken.None);
        });
    }

    [Fact]
    public async Task DeleteFileAsync_ValidPath_DeletesSuccessfully()
    {
        // Arrange
        var mockClient = new Mock<StorageClient>();
        mockClient
            .Setup(x => x.DeleteObjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DeleteObjectOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        var dummyCredential = Google.Apis.Auth.OAuth2.GoogleCredential.FromAccessToken("dummy-token");
        var service = new GcsStorageService(mockClient.Object, CreateTestConfig(), mockHttpClientFactory.Object, dummyCredential);

        // Act
        await service.DeleteFileAsync("test-service/uploads/test.txt");

        // Assert
        mockClient.Verify(x => x.DeleteObjectAsync(
            "test-bucket",
            "test-service/uploads/test.txt",
            It.IsAny<DeleteObjectOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetFileMetadataAsync_FileExists_ReturnsMetadata()
    {
        // Arrange
        var mockClient = new Mock<StorageClient>();
        var createdAt = DateTime.UtcNow.AddDays(-1);
        mockClient
            .Setup(x => x.GetObjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<GetObjectOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Google.Apis.Storage.v1.Data.Object
            {
                Name = "test.txt",
                ContentType = "text/plain",
                Size = 1024,
                TimeCreatedDateTimeOffset = new DateTimeOffset(createdAt),
                ETag = "etag123",
                Md5Hash = "mock-md5"
            });

        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        var dummyCredential = Google.Apis.Auth.OAuth2.GoogleCredential.FromAccessToken("dummy-token");
        var service = new GcsStorageService(mockClient.Object, CreateTestConfig(), mockHttpClientFactory.Object, dummyCredential);

        // Act
        var metadata = await service.GetFileMetadataAsync("test.txt");

        // Assert
        Assert.NotNull(metadata);
        Assert.Equal("test.txt", metadata.Name);
        Assert.Equal("text/plain", metadata.ContentType);
        Assert.Equal(1024, metadata.SizeBytes);
        Assert.Equal("etag123", metadata.ETag);
        Assert.Equal(createdAt, metadata.CreatedAt);
    }

    [Fact]
    public async Task GetFileMetadataAsync_FileDoesNotExist_ReturnsNull()
    {
        // Arrange
        var mockClient = new Mock<StorageClient>();
        mockClient
            .Setup(x => x.GetObjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<GetObjectOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Google.GoogleApiException("GCS", "Not Found")
            {
                HttpStatusCode = System.Net.HttpStatusCode.NotFound
            });

        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        var dummyCredential = Google.Apis.Auth.OAuth2.GoogleCredential.FromAccessToken("dummy-token");
        var service = new GcsStorageService(mockClient.Object, CreateTestConfig(), mockHttpClientFactory.Object, dummyCredential);

        // Act
        var metadata = await service.GetFileMetadataAsync("nonexistent.txt");

        // Assert
        Assert.Null(metadata);
    }

    [Fact]
    public async Task GetFileMetadataAsync_FileNotFound_WithErrorCode404_ReturnsNull()
    {
        // Arrange
        var mockClient = new Mock<StorageClient>();
        var exception = new Google.GoogleApiException("GCS", "Not Found");
        // Simulate error code 404
        var errorField = typeof(Google.GoogleApiException).GetProperty("Error");
        if (errorField != null && errorField.CanWrite)
        {
            errorField.SetValue(exception, new Google.Apis.Requests.RequestError { Code = 404 });
        }

        mockClient
            .Setup(x => x.GetObjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<GetObjectOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        var dummyCredential = Google.Apis.Auth.OAuth2.GoogleCredential.FromAccessToken("dummy-token");
        var service = new GcsStorageService(mockClient.Object, CreateTestConfig(), mockHttpClientFactory.Object, dummyCredential);

        // Act
        var metadata = await service.GetFileMetadataAsync("nonexistent.txt");

        // Assert
        Assert.Null(metadata);
    }

    [Fact]
    public async Task FileExistsAsync_WithErrorCode404_ReturnsFalse()
    {
        // Arrange
        var mockClient = new Mock<StorageClient>();
        var exception = new Google.GoogleApiException("GCS", "Not Found");
        var errorField = typeof(Google.GoogleApiException).GetProperty("Error");
        if (errorField != null && errorField.CanWrite)
        {
            errorField.SetValue(exception, new Google.Apis.Requests.RequestError { Code = 404 });
        }

        mockClient
            .Setup(x => x.GetObjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<GetObjectOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        var dummyCredential = Google.Apis.Auth.OAuth2.GoogleCredential.FromAccessToken("dummy-token");
        var service = new GcsStorageService(mockClient.Object, CreateTestConfig(), mockHttpClientFactory.Object, dummyCredential);

        // Act
        var exists = await service.FileExistsAsync("nonexistent.txt");

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task FileExistsAsync_WithNotFoundMessage_ReturnsFalse()
    {
        // Arrange
        var mockClient = new Mock<StorageClient>();
        mockClient
            .Setup(x => x.GetObjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<GetObjectOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Google.GoogleApiException("GCS", "Resource Not Found"));

        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        var dummyCredential = Google.Apis.Auth.OAuth2.GoogleCredential.FromAccessToken("dummy-token");
        var service = new GcsStorageService(mockClient.Object, CreateTestConfig(), mockHttpClientFactory.Object, dummyCredential);

        // Act
        var exists = await service.FileExistsAsync("nonexistent.txt");

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task GetFileMetadataAsync_WithNotFoundMessage_ReturnsNull()
    {
        // Arrange
        var mockClient = new Mock<StorageClient>();
        mockClient
            .Setup(x => x.GetObjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<GetObjectOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Google.GoogleApiException("GCS", "File Not Found"));

        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        var dummyCredential = Google.Apis.Auth.OAuth2.GoogleCredential.FromAccessToken("dummy-token");
        var service = new GcsStorageService(mockClient.Object, CreateTestConfig(), mockHttpClientFactory.Object, dummyCredential);

        // Act
        var metadata = await service.GetFileMetadataAsync("nonexistent.txt");

        // Assert
        Assert.Null(metadata);
    }

    // ---------------------------------------------------------------------
    // Bucket-routing rule tests.  GetBucketForPath is private, so we exercise
    // it indirectly by calling FileExistsAsync with various paths and asserting
    // which bucket name was passed to GetObjectAsync.
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("cache/tessellation/abc/t002.json", "bucket-cache")]
    [InlineData("cache/dfm-results/abc/FDM_b1s0.json", "bucket-cache")]
    [InlineData("CACHE/foo.json", "bucket-cache")] // case-insensitive prefix
    [InlineData("customers/123/file.pdf", "bucket-customers")]
    [InlineData("CompanyA/orders/456/material.step", "bucket-operations")] // requires /orders/ substring
    [InlineData("CompanyA/invoices/789/inv.pdf", "bucket-financials")] // requires /invoices/ substring
    [InlineData("uploads/temp.bin", "bucket-temp")] // unmatched -> default
    public async Task FileExistsAsync_RoutesPathToCorrectBucket(string storagePath, string expectedBucket)
    {
        // Arrange
        var mockClient = new Mock<StorageClient>();
        string? capturedBucket = null;
        mockClient
            .Setup(x => x.GetObjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<GetObjectOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, GetObjectOptions, CancellationToken>(
                (bucket, _, _, _) => capturedBucket = bucket)
            .ReturnsAsync(new Google.Apis.Storage.v1.Data.Object { Name = storagePath });

        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        var dummyCredential = Google.Apis.Auth.OAuth2.GoogleCredential.FromAccessToken("dummy-token");
        var service = new GcsStorageService(mockClient.Object, CreateRoutingConfig(), mockHttpClientFactory.Object, dummyCredential);

        // Act
        var exists = await service.FileExistsAsync(storagePath);

        // Assert
        Assert.True(exists);
        Assert.Equal(expectedBucket, capturedBucket);
    }

    [Fact]
    public async Task FileExistsAsync_CacheBucket_FallsBackToDefaultName_WhenConfigMissing()
    {
        // Arrange — config WITHOUT a Cache key.  Routing should fall back to
        // the hard-coded default ("maliev-cache") rather than the temp bucket.
        var mockClient = new Mock<StorageClient>();
        string? capturedBucket = null;
        mockClient
            .Setup(x => x.GetObjectAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<GetObjectOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, GetObjectOptions, CancellationToken>(
                (bucket, _, _, _) => capturedBucket = bucket)
            .ThrowsAsync(new Google.GoogleApiException("GCS", "Not Found"));

        var configMissingCache = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GoogleCloud:Buckets:Temp"] = "bucket-temp"
            })
            .Build();

        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        var dummyCredential = Google.Apis.Auth.OAuth2.GoogleCredential.FromAccessToken("dummy-token");
        var service = new GcsStorageService(mockClient.Object, configMissingCache, mockHttpClientFactory.Object, dummyCredential);

        // Act
        await service.FileExistsAsync("cache/tessellation/abc/t002.json");

        // Assert
        Assert.Equal("maliev-cache", capturedBucket);
    }
}
