using System.Collections.Concurrent;
using Maliev.UploadService.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Maliev.UploadService.Infrastructure.Storage;

/// <summary>
/// Mock storage service for testing environments when Google Cloud is disabled.
/// </summary>
public class MockStorageService : IStorageService
{
    private static readonly ConcurrentDictionary<string, MockStoredObject> StoredObjects = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, MockUploadSession> ResumableSessions = new(StringComparer.OrdinalIgnoreCase);

    private readonly ILogger<MockStorageService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="MockStorageService"/>.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public MockStorageService(ILogger<MockStorageService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<StorageUploadResult> UploadFileAsync(
        Stream fileStream,
        string storagePath,
        string contentType,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MOCK: Uploading file to {StoragePath}", storagePath);
        await Task.CompletedTask;
        var uploadedAt = DateTime.UtcNow;
        var eTag = Guid.NewGuid().ToString();
        StoredObjects[storagePath] = new MockStoredObject(storagePath, contentType, fileStream.Length, uploadedAt, eTag, "mock-md5");
        return new StorageUploadResult
        {
            StoragePath = storagePath,
            ContentType = contentType,
            SizeBytes = fileStream.Length,
            UploadedAt = uploadedAt,
            ETag = eTag,
            Md5Hash = "mock-md5"
        };
    }

    /// <inheritdoc/>
    public Task<bool> FileExistsAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MOCK: Checking existence of file {StoragePath}", storagePath);
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task DeleteFileAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MOCK: Deleting file {StoragePath}", storagePath);
        StoredObjects.TryRemove(storagePath, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<string> GenerateSignedUrlAsync(
        string storagePath,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MOCK: Generating signed URL for {StoragePath}", storagePath);
        return Task.FromResult($"https://mock-storage.local/{storagePath}?token=mock-token");
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public Task<ResumableUploadProgress> ResumeUploadAsync(
        string sessionUri,
        Stream chunkStream,
        long startByte,
        long endByte,
        long totalSize,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MOCK: Resuming upload for {SessionUri}", sessionUri);
        var bytesReceived = endByte + 1;
        var isComplete = bytesReceived >= totalSize;
        var storagePath = "mock-path";
        if (ResumableSessions.TryGetValue(sessionUri, out var session))
        {
            storagePath = session.StoragePath;
            if (isComplete)
            {
                StoredObjects[session.StoragePath] = new MockStoredObject(
                    session.StoragePath,
                    session.ContentType,
                    bytesReceived,
                    DateTime.UtcNow,
                    Guid.NewGuid().ToString(),
                    "mock-md5");
            }
        }

        return Task.FromResult(new ResumableUploadProgress
        {
            BytesReceived = bytesReceived,
            TotalSize = totalSize,
            IsComplete = isComplete,
            StoragePath = isComplete ? storagePath : null
        });
    }

    /// <inheritdoc/>
    public Task UpdateStorageClassAsync(string storagePath, string targetStorageClass, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MOCK: Updating storage class of {StoragePath} to {StorageClass}", storagePath, targetStorageClass);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<StorageUploadResult> CopyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MOCK: Copying file from {SourcePath} to {DestPath}", sourcePath, destinationPath);
        return Task.FromResult(new StorageUploadResult
        {
            StoragePath = destinationPath,
            ContentType = "application/octet-stream",
            SizeBytes = 0,
            UploadedAt = DateTime.UtcNow,
            ETag = Guid.NewGuid().ToString(),
            Md5Hash = "mock-md5"
        });
    }

    private sealed record MockUploadSession(string StoragePath, string ContentType, long TotalSize);

    private sealed record MockStoredObject(
        string StoragePath,
        string ContentType,
        long SizeBytes,
        DateTime CreatedAt,
        string ETag,
        string Md5Hash);
}
