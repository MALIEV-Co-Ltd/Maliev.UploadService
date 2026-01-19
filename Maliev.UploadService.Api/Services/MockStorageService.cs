namespace Maliev.UploadService.Api.Services;

/// <summary>
/// Mock storage service for testing environments.
/// </summary>
public class MockStorageService : IStorageService
{
    private readonly ILogger<MockStorageService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockStorageService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public MockStorageService(ILogger<MockStorageService> logger)
    {
        _logger = logger;
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
        await Task.CompletedTask;
        return new StorageUploadResult
        {
            StoragePath = storagePath,
            ContentType = contentType,
            SizeBytes = fileStream.Length,
            UploadedAt = DateTime.UtcNow,
            ETag = Guid.NewGuid().ToString()
        };
    }

    /// <inheritdoc />
    public Task<bool> FileExistsAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MOCK: Checking existence of file {StoragePath}", storagePath);
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task DeleteFileAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MOCK: Deleting file {StoragePath}", storagePath);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<string> GenerateSignedUrlAsync(
        string storagePath,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MOCK: Generating signed URL for {StoragePath}", storagePath);
        return Task.FromResult($"https://mock-storage.local/{storagePath}?token=mock-token");
    }

    /// <inheritdoc />
    public Task<StorageFileMetadata?> GetFileMetadataAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MOCK: Getting metadata for {StoragePath}", storagePath);
        return Task.FromResult<StorageFileMetadata?>(new StorageFileMetadata
        {
            Name = storagePath,
            ContentType = "application/octet-stream",
            SizeBytes = 100,
            CreatedAt = DateTime.UtcNow,
            ETag = "mock-etag"
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
        return Task.FromResult(new ResumableUploadSession
        {
            SessionUri = $"https://mock-storage.local/upload/{Guid.NewGuid()}",
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
        _logger.LogInformation("MOCK: Resuming upload for {SessionUri}", sessionUri);
        return Task.FromResult(new ResumableUploadProgress
        {
            BytesReceived = endByte + 1,
            TotalSize = totalSize,
            IsComplete = endByte + 1 >= totalSize,
            StoragePath = endByte + 1 >= totalSize ? "mock-path" : null
        });
    }
}
