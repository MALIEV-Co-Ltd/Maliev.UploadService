using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;

namespace Maliev.UploadService.Api.Services;

/// <summary>
/// Mock storage service for testing environments.
/// </summary>
public class MockStorageService : IStorageService
{
    private static readonly ConcurrentDictionary<string, MockStoredObject> StoredObjects = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, MockUploadSession> ResumableSessions = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, MockSignedUrl> SignedUrls = new(StringComparer.Ordinal);

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<MockStorageService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockStorageService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="httpContextAccessor">Accessor for the current request.</param>
    public MockStorageService(
        ILogger<MockStorageService> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public async Task<StorageUploadResult> UploadFileAsync(
        Stream fileStream,
        string storagePath,
        string contentType,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MOCK: Uploading file to {StoragePath}", storagePath);

        using var memory = new MemoryStream();
        await fileStream.CopyToAsync(memory, cancellationToken);
        var content = memory.ToArray();

        var uploadedAt = DateTime.UtcNow;
        var eTag = Guid.NewGuid().ToString();
        StoredObjects[storagePath] = new MockStoredObject(storagePath, contentType, content.LongLength, uploadedAt, eTag, "mock-md5", content);
        return new StorageUploadResult
        {
            StoragePath = storagePath,
            ContentType = contentType,
            SizeBytes = content.LongLength,
            UploadedAt = uploadedAt,
            ETag = eTag,
            Md5Hash = "mock-md5"
        };
    }

    /// <inheritdoc />
    public Task<bool> FileExistsAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MOCK: Checking existence of file {StoragePath}", storagePath);
        return Task.FromResult(StoredObjects.ContainsKey(storagePath));
    }

    /// <inheritdoc />
    public Task DeleteFileAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MOCK: Deleting file {StoragePath}", storagePath);
        StoredObjects.TryRemove(storagePath, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string> GenerateSignedUrlAsync(
        string storagePath,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MOCK: Generating signed URL for {StoragePath}", storagePath);

        if (!StoredObjects.ContainsKey(storagePath))
        {
            throw new FileNotFoundException($"Mock storage object was not found: {storagePath}", storagePath);
        }

        var token = Guid.NewGuid().ToString("N");
        SignedUrls[token] = new MockSignedUrl(storagePath, DateTimeOffset.UtcNow.Add(expiration));
        return Task.FromResult(BuildMockSignedUrl(token));
    }

    /// <inheritdoc />
    public Task<StorageFileMetadata?> GetFileMetadataAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MOCK: Getting metadata for {StoragePath}", storagePath);
        if (StoredObjects.TryGetValue(storagePath, out var storedObject))
        {
            return Task.FromResult<StorageFileMetadata?>(new StorageFileMetadata
            {
                Name = storedObject.StoragePath,
                ContentType = storedObject.ContentType,
                SizeBytes = storedObject.SizeBytes,
                CreatedAt = storedObject.CreatedAt,
                ETag = storedObject.ETag,
                Md5Hash = storedObject.Md5Hash
            });
        }

        return Task.FromResult<StorageFileMetadata?>(new StorageFileMetadata
        {
            Name = storagePath,
            ContentType = "application/octet-stream",
            SizeBytes = 100,
            CreatedAt = DateTime.UtcNow,
            ETag = "mock-etag",
            Md5Hash = "mock-md5-hash"
        });
    }

    /// <inheritdoc />
    public Task<ResumableUploadSession> InitiateResumableUploadAsync(
        string storagePath,
        string contentType,
        long totalSize,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MOCK: Initiating resumable upload for {StoragePath}", storagePath);
        var sessionUri = $"https://mock-storage.local/upload/{Guid.NewGuid()}";
        ResumableSessions[sessionUri] = new MockUploadSession(storagePath, contentType, totalSize);
        return Task.FromResult(new ResumableUploadSession
        {
            SessionUri = sessionUri,
            StoragePath = storagePath,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        });
    }

    /// <inheritdoc />
    public Task<ResumableUploadProgress> ResumeUploadAsync(
        string sessionUri,
        Stream chunkStream,
        long startByte,
        long endByte,
        long totalSize,
        CancellationToken cancellationToken = default)
    {
        return ResumeUploadCoreAsync(sessionUri, chunkStream, startByte, endByte, totalSize, cancellationToken);
    }

    /// <summary>
    /// Gets a signed mock object by token.
    /// </summary>
    /// <param name="token">The opaque signed URL token.</param>
    /// <param name="content">The stored object content.</param>
    /// <param name="contentType">The stored object content type.</param>
    /// <param name="storagePath">The stored object path.</param>
    /// <returns><c>true</c> when the token maps to an unexpired object; otherwise <c>false</c>.</returns>
    public static bool TryGetSignedObject(
        string token,
        out byte[] content,
        out string contentType,
        out string storagePath)
    {
        content = [];
        contentType = string.Empty;
        storagePath = string.Empty;

        if (!SignedUrls.TryGetValue(token, out var signedUrl))
        {
            return false;
        }

        if (signedUrl.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            SignedUrls.TryRemove(token, out _);
            return false;
        }

        if (!StoredObjects.TryGetValue(signedUrl.StoragePath, out var storedObject))
        {
            return false;
        }

        content = storedObject.Content;
        contentType = storedObject.ContentType;
        storagePath = storedObject.StoragePath;
        return true;
    }

    /// <inheritdoc />
    public Task UpdateStorageClassAsync(string storagePath, string targetStorageClass, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MOCK: Updating storage class of {StoragePath} to {StorageClass}", storagePath, targetStorageClass);
        return Task.CompletedTask;
    }

    private async Task<ResumableUploadProgress> ResumeUploadCoreAsync(
        string sessionUri,
        Stream chunkStream,
        long startByte,
        long endByte,
        long totalSize,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("MOCK: Resuming upload for {SessionUri}", sessionUri);
        var bytesReceived = endByte + 1;
        var isComplete = bytesReceived >= totalSize;
        var storagePath = "mock-path";
        if (ResumableSessions.TryGetValue(sessionUri, out var session))
        {
            storagePath = session.StoragePath;

            using var memory = new MemoryStream();
            await chunkStream.CopyToAsync(memory, cancellationToken);
            session.WriteChunk(memory.ToArray(), startByte);

            if (isComplete)
            {
                var content = session.ReadContent();
                StoredObjects[session.StoragePath] = new MockStoredObject(
                    session.StoragePath,
                    session.ContentType,
                    content.LongLength,
                    DateTime.UtcNow,
                    Guid.NewGuid().ToString(),
                    "mock-md5",
                    content);
            }
        }

        return new ResumableUploadProgress
        {
            BytesReceived = bytesReceived,
            TotalSize = totalSize,
            IsComplete = isComplete,
            StoragePath = isComplete ? storagePath : null
        };
    }

    private string BuildMockSignedUrl(string token)
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request?.Host.HasValue == true)
        {
            var pathBase = request.PathBase.HasValue ? request.PathBase.Value : string.Empty;
            return $"{request.Scheme}://{request.Host}{pathBase}/upload/v1/mock-storage/{token}";
        }

        return $"http://localhost/upload/v1/mock-storage/{token}";
    }

    private sealed record MockSignedUrl(string StoragePath, DateTimeOffset ExpiresAt);

    private sealed class MockUploadSession
    {
        private readonly MemoryStream _content = new();

        public MockUploadSession(string storagePath, string contentType, long totalSize)
        {
            StoragePath = storagePath;
            ContentType = contentType;
            TotalSize = totalSize;
        }

        public string StoragePath { get; }

        public string ContentType { get; }

        public long TotalSize { get; }

        public void WriteChunk(byte[] chunk, long startByte)
        {
            _content.Position = startByte;
            _content.Write(chunk);
        }

        public byte[] ReadContent()
        {
            return _content.ToArray();
        }
    }

    private sealed record MockStoredObject(
        string StoragePath,
        string ContentType,
        long SizeBytes,
        DateTime CreatedAt,
        string ETag,
        string Md5Hash,
        byte[] Content);
}
