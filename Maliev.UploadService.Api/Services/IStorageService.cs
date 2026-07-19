namespace Maliev.UploadService.Api.Services;

/// <summary>
/// Interface for storage operations.
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Uploads a file to storage.
    /// </summary>
    /// <param name="fileStream">The file stream.</param>
    /// <param name="storagePath">The storage path.</param>
    /// <param name="contentType">The content type.</param>
    /// <param name="overwrite">Whether to overwrite an existing file.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The upload result.</returns>
    Task<StorageUploadResult> UploadFileAsync(
        Stream fileStream,
        string storagePath,
        string contentType,
        bool overwrite = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a file exists at the specified path.
    /// </summary>
    /// <param name="storagePath">The storage path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the file exists, otherwise false.</returns>
    Task<bool> FileExistsAsync(string storagePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file from storage.
    /// </summary>
    /// <param name="storagePath">The storage path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteFileAsync(string storagePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a signed URL for file download.
    /// </summary>
    /// <param name="storagePath">The storage path.</param>
    /// <param name="expiration">The expiration time span.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The signed URL.</returns>
    Task<string> GenerateSignedUrlAsync(
        string storagePath,
        TimeSpan expiration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets file metadata from storage.
    /// </summary>
    /// <param name="storagePath">The storage path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The file metadata if found, otherwise null.</returns>
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
    /// <param name="storagePath">The storage path.</param>
    /// <param name="targetStorageClass">The target storage class.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateStorageClassAsync(string storagePath, string targetStorageClass, CancellationToken cancellationToken = default);
}

/// <summary>
/// Metadata for a file in storage.
/// </summary>
public class StorageFileMetadata
{
    /// <summary>
    /// Gets or sets the name of the file.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the content type of the file.
    /// </summary>
    public required string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the size of the file in bytes.
    /// </summary>
    public required long SizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp.
    /// </summary>
    public required DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the ETag of the file.
    /// </summary>
    public required string ETag { get; set; }

    /// <summary>
    /// Gets or sets the MD5 hash of the file.
    /// </summary>
    public string? Md5Hash { get; set; }
}

/// <summary>
/// Result of a storage upload operation.
/// </summary>
public class StorageUploadResult
{
    /// <summary>
    /// Gets or sets the storage path of the uploaded file.
    /// </summary>
    public required string StoragePath { get; set; }

    /// <summary>
    /// Gets or sets the content type of the file.
    /// </summary>
    public required string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the size of the file in bytes.
    /// </summary>
    public required long SizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the upload timestamp.
    /// </summary>
    public required DateTime UploadedAt { get; set; }

    /// <summary>
    /// Gets or sets the ETag of the uploaded file.
    /// </summary>
    public required string ETag { get; set; }

    /// <summary>
    /// Gets or sets the MD5 hash of the uploaded file.
    /// </summary>
    public string? Md5Hash { get; set; }
}

/// <summary>
/// Session information for a resumable upload.
/// </summary>
public class ResumableUploadSession
{
    /// <summary>
    /// Gets or sets the session URI for the resumable upload.
    /// </summary>
    public required string SessionUri { get; set; }

    /// <summary>
    /// Gets or sets the storage path of the file.
    /// </summary>
    public required string StoragePath { get; set; }

    /// <summary>
    /// Gets or sets the session expiration timestamp.
    /// </summary>
    public required DateTime ExpiresAt { get; set; }
}

/// <summary>
/// Progress information for a resumable upload.
/// </summary>
public class ResumableUploadProgress
{
    /// <summary>
    /// Gets or sets the number of bytes received.
    /// </summary>
    public required long BytesReceived { get; set; }

    /// <summary>
    /// Gets or sets the total size of the file.
    /// </summary>
    public required long TotalSize { get; set; }

    /// <summary>
    /// Gets or sets whether the upload is complete.
    /// </summary>
    public required bool IsComplete { get; set; }

    /// <summary>
    /// Gets or sets the storage path of the uploaded file (when complete).
    /// </summary>
    public string? StoragePath { get; set; }
}
