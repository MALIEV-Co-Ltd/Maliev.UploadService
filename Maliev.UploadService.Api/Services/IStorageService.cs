namespace Maliev.UploadService.Api.Services;

public interface IStorageService
{
    Task<StorageUploadResult> UploadFileAsync(
        Stream fileStream,
        string storagePath,
        string contentType,
        bool overwrite = false,
        CancellationToken cancellationToken = default);

    Task<bool> FileExistsAsync(string storagePath, CancellationToken cancellationToken = default);

    Task DeleteFileAsync(string storagePath, CancellationToken cancellationToken = default);

    Task<string> GenerateSignedUrlAsync(
        string storagePath,
        TimeSpan expiration,
        CancellationToken cancellationToken = default);

    Task<StorageFileMetadata?> GetFileMetadataAsync(string storagePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Initiates a resumable upload session (FR-022, FR-024)
    /// </summary>
    Task<ResumableUploadSession> InitiateResumableUploadAsync(
        string storagePath,
        string contentType,
        long totalSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes an upload with a chunk of data (FR-022, FR-024)
    /// </summary>
    Task<ResumableUploadProgress> ResumeUploadAsync(
        string sessionUri,
        Stream chunkStream,
        long startByte,
        long endByte,
        long totalSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the storage class of an existing file (FR-015)
    /// </summary>
    Task UpdateStorageClassAsync(string storagePath, string targetStorageClass, CancellationToken cancellationToken = default);
}

public class StorageFileMetadata
{
    public required string Name { get; set; }
    public required string ContentType { get; set; }
    public required long SizeBytes { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required string ETag { get; set; }
    public string? Md5Hash { get; set; }
}

public class StorageUploadResult
{
    public required string StoragePath { get; set; }
    public required string ContentType { get; set; }
    public required long SizeBytes { get; set; }
    public required DateTime UploadedAt { get; set; }
    public required string ETag { get; set; }
    public string? Md5Hash { get; set; }
}

public class ResumableUploadSession
{
    public required string SessionUri { get; set; }
    public required string StoragePath { get; set; }
    public required DateTime ExpiresAt { get; set; }
}

public class ResumableUploadProgress
{
    public required long BytesReceived { get; set; }
    public required long TotalSize { get; set; }
    public required bool IsComplete { get; set; }
    public string? StoragePath { get; set; }
}
