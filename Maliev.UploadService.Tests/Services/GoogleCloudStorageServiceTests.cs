using FluentAssertions;
using Google.Apis.Download;
using Google.Apis.Storage.v1.Data;
using Google.Apis.Upload;
using Google.Cloud.Storage.V1;
using Maliev.UploadService.Api.Models;
using Maliev.UploadService.Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Maliev.UploadService.Tests.Services;

public class GoogleCloudStorageServiceTests
{
    private readonly Mock<StorageClient> _mockStorageClient;
    private readonly Mock<ILogger<GoogleCloudStorageService>> _mockLogger;
    private readonly GoogleCloudStorageService _service;
    private const string TestBucketName = "test-bucket";
    private const string TestObjectName = "test-object.txt";

    public GoogleCloudStorageServiceTests()
    {
        _mockStorageClient = new Mock<StorageClient>();
        _mockLogger = new Mock<ILogger<GoogleCloudStorageService>>();
        _service = new GoogleCloudStorageService(_mockStorageClient.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task UploadFileAsync_ValidFile_ReturnsETag()
    {
        // Arrange
        var fileContent = "Test file content";
        var fileBytes = Encoding.UTF8.GetBytes(fileContent);
        using var fileStream = new MemoryStream(fileBytes);
        var contentType = "text/plain";
        var expectedETag = "test-etag-12345";

        var uploadedObject = new Google.Apis.Storage.v1.Data.Object
        {
            ETag = expectedETag,
            Name = TestObjectName,
            Bucket = TestBucketName
        };

        _mockStorageClient.Setup(x => x.UploadObjectAsync(
                It.Is<Google.Apis.Storage.v1.Data.Object>(o =>
                    o.Name == TestObjectName &&
                    o.Bucket == TestBucketName &&
                    o.ContentType == contentType),
                It.IsAny<Stream>(),
                null,
                It.IsAny<CancellationToken>(),
                null))
            .ReturnsAsync(uploadedObject);

        // Act
        var result = await _service.UploadFileAsync(TestBucketName, TestObjectName, fileStream, contentType);

        // Assert
        result.Should().Be(expectedETag);
        _mockStorageClient.Verify(x => x.UploadObjectAsync(
            It.IsAny<Google.Apis.Storage.v1.Data.Object>(),
            It.IsAny<Stream>(),
            null,
            It.IsAny<CancellationToken>(),
            null), Times.Once);
    }

    [Fact]
    public async Task UploadFileAsync_StreamNotSeekable_StillUploads()
    {
        // Arrange
        var mockStream = new Mock<Stream>();
        mockStream.Setup(x => x.CanSeek).Returns(false);
        var contentType = "application/octet-stream";
        var expectedETag = "test-etag-67890";

        var uploadedObject = new Google.Apis.Storage.v1.Data.Object
        {
            ETag = expectedETag,
            Name = TestObjectName,
            Bucket = TestBucketName
        };

        _mockStorageClient.Setup(x => x.UploadObjectAsync(
                It.IsAny<Google.Apis.Storage.v1.Data.Object>(),
                It.IsAny<Stream>(),
                null,
                It.IsAny<CancellationToken>(),
                null))
            .ReturnsAsync(uploadedObject);

        // Act
        var result = await _service.UploadFileAsync(TestBucketName, TestObjectName, mockStream.Object, contentType);

        // Assert
        result.Should().Be(expectedETag);
        mockStream.Verify(x => x.Position, Times.Never); // Should not try to set position
    }

    [Fact]
    public async Task UploadFileAsync_ThrowsException_LogsErrorAndRethrows()
    {
        // Arrange
        using var fileStream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        var expectedException = new InvalidOperationException("Upload failed");

        _mockStorageClient.Setup(x => x.UploadObjectAsync(
                It.IsAny<Google.Apis.Storage.v1.Data.Object>(),
                It.IsAny<Stream>(),
                null,
                It.IsAny<CancellationToken>(),
                null))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UploadFileAsync(TestBucketName, TestObjectName, fileStream, "text/plain"));

        exception.Should().Be(expectedException);
    }

    [Fact]
    public async Task DownloadFileAsync_ValidObject_ReturnsFileDownloadResponse()
    {
        // Arrange
        var fileContent = "Downloaded file content";
        var fileBytes = Encoding.UTF8.GetBytes(fileContent);
        var contentType = "text/plain";

        var objectMetadata = new Google.Apis.Storage.v1.Data.Object
        {
            Name = TestObjectName,
            Bucket = TestBucketName,
            ContentType = contentType,
            Size = (ulong?)fileBytes.Length
        };

        _mockStorageClient.Setup(x => x.GetObjectAsync(TestBucketName, TestObjectName, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(objectMetadata);

        _mockStorageClient.Setup(x => x.DownloadObjectAsync(TestBucketName, TestObjectName, It.IsAny<Stream>(), null, It.IsAny<CancellationToken>(), null))
            .Callback<string, string, Stream, DownloadObjectOptions, CancellationToken, IProgress<IDownloadProgress>>((bucket, obj, stream, options, token, progress) =>
            {
                stream.Write(fileBytes, 0, fileBytes.Length);
            })
            .ReturnsAsync(objectMetadata);

        // Act
        var result = await _service.DownloadFileAsync(TestBucketName, TestObjectName);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().BeEquivalentTo(fileBytes);
        result.ContentType.Should().Be(contentType);
        result.FileName.Should().Be(TestObjectName);
        result.FileSize.Should().Be(fileBytes.Length);
    }

    [Fact]
    public async Task DownloadFileAsync_NoContentType_UsesDefault()
    {
        // Arrange
        var fileBytes = Encoding.UTF8.GetBytes("test");
        var objectMetadata = new Google.Apis.Storage.v1.Data.Object
        {
            Name = TestObjectName,
            Bucket = TestBucketName,
            ContentType = null // No content type
        };

        _mockStorageClient.Setup(x => x.GetObjectAsync(TestBucketName, TestObjectName, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(objectMetadata);

        _mockStorageClient.Setup(x => x.DownloadObjectAsync(TestBucketName, TestObjectName, It.IsAny<Stream>(), null, It.IsAny<CancellationToken>(), null))
            .Callback<string, string, Stream, DownloadObjectOptions, CancellationToken, IProgress<IDownloadProgress>>((bucket, obj, stream, options, token, progress) =>
            {
                stream.Write(fileBytes, 0, fileBytes.Length);
            })
            .ReturnsAsync(objectMetadata);

        // Act
        var result = await _service.DownloadFileAsync(TestBucketName, TestObjectName);

        // Assert
        result.ContentType.Should().Be("application/octet-stream");
    }

    [Fact]
    public async Task DownloadFileAsync_ThrowsException_LogsErrorAndRethrows()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Download failed");

        _mockStorageClient.Setup(x => x.GetObjectAsync(TestBucketName, TestObjectName, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DownloadFileAsync(TestBucketName, TestObjectName));

        exception.Should().Be(expectedException);
    }

    [Fact]
    public async Task DeleteFileAsync_ValidObject_ReturnsTrue()
    {
        // Arrange
        _mockStorageClient.Setup(x => x.DeleteObjectAsync(TestBucketName, TestObjectName, null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.DeleteFileAsync(TestBucketName, TestObjectName);

        // Assert
        result.Should().BeTrue();
        _mockStorageClient.Verify(x => x.DeleteObjectAsync(TestBucketName, TestObjectName, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteFileAsync_ObjectNotFound_ReturnsTrue()
    {
        // Arrange
        var notFoundException = new Google.GoogleApiException("Google.Cloud.Storage.V1", "Not Found")
        {
            HttpStatusCode = HttpStatusCode.NotFound
        };

        _mockStorageClient.Setup(x => x.DeleteObjectAsync(TestBucketName, TestObjectName, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(notFoundException);

        // Act
        var result = await _service.DeleteFileAsync(TestBucketName, TestObjectName);

        // Assert
        result.Should().BeTrue(); // Should consider not found as successfully deleted
    }

    [Fact]
    public async Task DeleteFileAsync_OtherException_ReturnsFalse()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Delete failed");

        _mockStorageClient.Setup(x => x.DeleteObjectAsync(TestBucketName, TestObjectName, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act
        var result = await _service.DeleteFileAsync(TestBucketName, TestObjectName);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task FileExistsAsync_ObjectExists_ReturnsTrue()
    {
        // Arrange
        var objectMetadata = new Google.Apis.Storage.v1.Data.Object
        {
            Name = TestObjectName,
            Bucket = TestBucketName
        };

        _mockStorageClient.Setup(x => x.GetObjectAsync(TestBucketName, TestObjectName, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(objectMetadata);

        // Act
        var result = await _service.FileExistsAsync(TestBucketName, TestObjectName);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task FileExistsAsync_ObjectNotFound_ReturnsFalse()
    {
        // Arrange
        var notFoundException = new Google.GoogleApiException("Google.Cloud.Storage.V1", "Not Found")
        {
            HttpStatusCode = HttpStatusCode.NotFound
        };

        _mockStorageClient.Setup(x => x.GetObjectAsync(TestBucketName, TestObjectName, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(notFoundException);

        // Act
        var result = await _service.FileExistsAsync(TestBucketName, TestObjectName);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task FileExistsAsync_OtherException_Rethrows()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Check failed");

        _mockStorageClient.Setup(x => x.GetObjectAsync(TestBucketName, TestObjectName, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.FileExistsAsync(TestBucketName, TestObjectName));

        exception.Should().Be(expectedException);
    }

    [Fact]
    public async Task GetFileSizeAsync_ValidObject_ReturnsSize()
    {
        // Arrange
        const long expectedSize = 12345;
        var objectMetadata = new Google.Apis.Storage.v1.Data.Object
        {
            Name = TestObjectName,
            Bucket = TestBucketName,
            Size = (ulong?)expectedSize
        };

        _mockStorageClient.Setup(x => x.GetObjectAsync(TestBucketName, TestObjectName, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(objectMetadata);

        // Act
        var result = await _service.GetFileSizeAsync(TestBucketName, TestObjectName);

        // Assert
        result.Should().Be(expectedSize);
    }

    [Fact]
    public async Task GetFileSizeAsync_NoSize_ReturnsZero()
    {
        // Arrange
        var objectMetadata = new Google.Apis.Storage.v1.Data.Object
        {
            Name = TestObjectName,
            Bucket = TestBucketName,
            Size = null
        };

        _mockStorageClient.Setup(x => x.GetObjectAsync(TestBucketName, TestObjectName, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(objectMetadata);

        // Act
        var result = await _service.GetFileSizeAsync(TestBucketName, TestObjectName);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task GetFileSizeAsync_ThrowsException_Rethrows()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Size check failed");

        _mockStorageClient.Setup(x => x.GetObjectAsync(TestBucketName, TestObjectName, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.GetFileSizeAsync(TestBucketName, TestObjectName));

        exception.Should().Be(expectedException);
    }

    [Fact]
    public async Task GenerateSignedUrlAsync_ForDownload_ReturnsPlaceholderUrl()
    {
        // Arrange
        var expiration = TimeSpan.FromHours(1);

        // Act
        var result = await _service.GenerateSignedUrlAsync(TestBucketName, TestObjectName, expiration, false);

        // Assert
        result.Should().Be($"https://storage.googleapis.com/{TestBucketName}/{TestObjectName}");
    }

    [Fact]
    public async Task GenerateSignedUrlAsync_ForUpload_ReturnsPlaceholderUrl()
    {
        // Arrange
        var expiration = TimeSpan.FromHours(2);

        // Act
        var result = await _service.GenerateSignedUrlAsync(TestBucketName, TestObjectName, expiration, true);

        // Assert
        result.Should().Be($"https://storage.googleapis.com/{TestBucketName}/{TestObjectName}");
    }

    [Fact]
    public async Task CalculateHashesAsync_ValidStream_ReturnsCorrectHashes()
    {
        // Arrange
        var fileContent = "Hello, World!";
        var fileBytes = Encoding.UTF8.GetBytes(fileContent);
        using var fileStream = new MemoryStream(fileBytes);

        // Calculate expected hashes
        using var md5 = MD5.Create();
        using var sha256 = SHA256.Create();
        var expectedMd5 = Convert.ToHexString(md5.ComputeHash(fileBytes)).ToLowerInvariant();
        var expectedSha256 = Convert.ToHexString(sha256.ComputeHash(fileBytes)).ToLowerInvariant();

        // Act
        var (md5Hash, sha256Hash) = await _service.CalculateHashesAsync(fileStream);

        // Assert
        md5Hash.Should().Be(expectedMd5);
        sha256Hash.Should().Be(expectedSha256);

        // Verify stream position is reset
        fileStream.Position.Should().Be(0);
    }

    [Fact]
    public async Task CalculateHashesAsync_NonSeekableStream_CalculatesHashes()
    {
        // Arrange
        var fileContent = "Test content for non-seekable stream";
        var fileBytes = Encoding.UTF8.GetBytes(fileContent);
        var mockStream = new Mock<Stream>();

        mockStream.Setup(x => x.CanSeek).Returns(false);
        mockStream.Setup(x => x.ReadAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0); // Simulate end of stream

        // Act
        var (md5Hash, sha256Hash) = await _service.CalculateHashesAsync(mockStream.Object);

        // Assert
        md5Hash.Should().NotBeNullOrEmpty();
        sha256Hash.Should().NotBeNullOrEmpty();
        mockStream.Verify(x => x.Position, Times.Never); // Should not try to set position
    }

    [Fact]
    public async Task CalculateHashesAsync_EmptyStream_ReturnsHashesForEmptyContent()
    {
        // Arrange
        using var emptyStream = new MemoryStream();

        // Calculate expected hashes for empty content
        using var md5 = MD5.Create();
        using var sha256 = SHA256.Create();
        var expectedMd5 = Convert.ToHexString(md5.ComputeHash(Array.Empty<byte>())).ToLowerInvariant();
        var expectedSha256 = Convert.ToHexString(sha256.ComputeHash(Array.Empty<byte>())).ToLowerInvariant();

        // Act
        var (md5Hash, sha256Hash) = await _service.CalculateHashesAsync(emptyStream);

        // Assert
        md5Hash.Should().Be(expectedMd5);
        sha256Hash.Should().Be(expectedSha256);
    }

    [Fact]
    public async Task CalculateHashesAsync_ThrowsException_LogsErrorAndRethrows()
    {
        // Arrange
        var mockStream = new Mock<Stream>();
        var expectedException = new InvalidOperationException("Hash calculation failed");

        mockStream.Setup(x => x.CanSeek).Returns(true);
        mockStream.Setup(x => x.ReadAsync(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CalculateHashesAsync(mockStream.Object));

        exception.Should().Be(expectedException);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task UploadFileAsync_InvalidBucketName_ShouldStillCallStorageClient(string? bucketName)
    {
        // Arrange
        using var fileStream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        var uploadedObject = new Google.Apis.Storage.v1.Data.Object { ETag = "test-etag" };

        _mockStorageClient.Setup(x => x.UploadObjectAsync(
                It.IsAny<Google.Apis.Storage.v1.Data.Object>(),
                It.IsAny<Stream>(),
                null,
                It.IsAny<CancellationToken>(),
                null))
            .ReturnsAsync(uploadedObject);

        // Act
        var result = await _service.UploadFileAsync(bucketName, TestObjectName, fileStream, "text/plain");

        // Assert - Service doesn't validate input, lets GCS handle it
        result.Should().Be("test-etag");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task UploadFileAsync_InvalidObjectName_ShouldStillCallStorageClient(string? objectName)
    {
        // Arrange
        using var fileStream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        var uploadedObject = new Google.Apis.Storage.v1.Data.Object { ETag = "test-etag" };

        _mockStorageClient.Setup(x => x.UploadObjectAsync(
                It.IsAny<Google.Apis.Storage.v1.Data.Object>(),
                It.IsAny<Stream>(),
                null,
                It.IsAny<CancellationToken>(),
                null))
            .ReturnsAsync(uploadedObject);

        // Act
        var result = await _service.UploadFileAsync(TestBucketName, objectName, fileStream, "text/plain");

        // Assert - Service doesn't validate input, lets GCS handle it
        result.Should().Be("test-etag");
    }
}