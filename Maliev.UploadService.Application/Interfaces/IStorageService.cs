namespace Maliev.UploadService.Application.Interfaces;

/// <summary>
/// Abstraction over the cloud object storage provider (GCS).
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Uploads a file stream to cloud storage.
    /// </summary>
    /// <param name="fileStream">The file content stream.</param>
    /// <param name="storagePath">The destination path in the bucket.</param>
    /// <param name="contentType">The MIME content type.</param>
    /// <param name="overwrite">Whether to overwrite an existing file at the same path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Upload result containing ETag, size, and path information.</returns>
    Task<StorageUploadResult> UploadFileAsync(
        Stream fileStream,
        string storagePath,
        string contentType,
        bool overwrite = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a file exists at the given storage path.
    /// </summary>
    /// <param name="storagePath">The storage path to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the file exists; otherwise <c>false</c>.</returns>
    Task<bool> FileExistsAsync(string storagePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes a file from cloud storage.
    /// </summary>
    /// <param name="storagePath">The storage path of the file to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteFileAsync(string storagePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a time-limited signed URL for direct file access.
    /// </summary>
    /// <param name="storagePath">The storage path of the file.</param>
    /// <param name="expiration">How long the signed URL should remain valid.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A signed URL string.</returns>
    Task<string> GenerateSignedUrlAsync(
        string storagePath,
        TimeSpan expiration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves storage-level metadata for a file (ETag, size, content type).
    /// </summary>
    /// <param name="storagePath">The storage path of the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>File metadata, or <c>null</c> if the file does not exist.</returns>
    Task<StorageFileMetadata?> GetFileMetadataAsync(string storagePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Initiates a GCS resumable upload session (FR-022, FR-024).
    /// </summary>
    /// <param name="storagePath">The destination path.</param>
    /// <param name="contentType">The MIME content type.</param>
    /// <param name="totalSize">Total size of the file in bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A resumable upload session containing a session URI.</returns>
    Task<ResumableUploadSession> InitiateResumableUploadAsync(
        string storagePath,
        string contentType,
        long totalSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a chunk of data to a resumable upload session (FR-022, FR-024).
    /// </summary>
    /// <param name="sessionUri">The GCS session URI.</param>
    /// <param name="chunkStream">The chunk data stream.</param>
    /// <param name="startByte">Start byte position (inclusive).</param>
    /// <param name="endByte">End byte position (inclusive).</param>
    /// <param name="totalSize">Total file size in bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Progress information including whether the upload is complete.</returns>
    Task<ResumableUploadProgress> ResumeUploadAsync(
        string sessionUri,
        Stream chunkStream,
        long startByte,
        long endByte,
        long totalSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the storage class of an existing file (FR-015).
    /// </summary>
    /// <param name="storagePath">The storage path of the file.</param>
    /// <param name="targetStorageClass">The target storage class (NEARLINE, COLDLINE, ARCHIVE).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateStorageClassAsync(string storagePath, string targetStorageClass, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents storage-level metadata for a file.
/// </summary>
public class StorageFileMetadata
{
    /// <summary>Gets or sets the object name in the bucket.</summary>
    public required string Name { get; set; }

    /// <summary>Gets or sets the MIME content type.</summary>
    public required string ContentType { get; set; }

    /// <summary>Gets or sets the file size in bytes.</summary>
    public required long SizeBytes { get; set; }

    /// <summary>Gets or sets when the object was created in storage.</summary>
    public required DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets the storage ETag.</summary>
    public required string ETag { get; set; }

    /// <summary>Gets or sets the Base64-encoded MD5 hash from the storage provider.</summary>
    public string? Md5Hash { get; set; }
}

/// <summary>
/// Represents the result of a successful file upload operation.
/// </summary>
public class StorageUploadResult
{
    /// <summary>Gets or sets the final storage path of the uploaded object.</summary>
    public required string StoragePath { get; set; }

    /// <summary>Gets or sets the MIME content type.</summary>
    public required string ContentType { get; set; }

    /// <summary>Gets or sets the file size in bytes.</summary>
    public required long SizeBytes { get; set; }

    /// <summary>Gets or sets when the object was uploaded.</summary>
    public required DateTime UploadedAt { get; set; }

    /// <summary>Gets or sets the storage ETag.</summary>
    public required string ETag { get; set; }

    /// <summary>Gets or sets the Base64-encoded MD5 hash from the storage provider.</summary>
    public string? Md5Hash { get; set; }
}

/// <summary>
/// Represents a GCS resumable upload session.
/// </summary>
public class ResumableUploadSession
{
    /// <summary>Gets or sets the GCS session URI used to send chunks.</summary>
    public required string SessionUri { get; set; }

    /// <summary>Gets or sets the destination storage path for this upload.</summary>
    public required string StoragePath { get; set; }

    /// <summary>Gets or sets when this session expires.</summary>
    public required DateTime ExpiresAt { get; set; }
}

/// <summary>
/// Represents the current progress of a resumable upload.
/// </summary>
public class ResumableUploadProgress
{
    /// <summary>Gets or sets the number of bytes received by the server.</summary>
    public required long BytesReceived { get; set; }

    /// <summary>Gets or sets the total expected file size.</summary>
    public required long TotalSize { get; set; }

    /// <summary>Gets or sets whether the upload is complete.</summary>
    public required bool IsComplete { get; set; }

    /// <summary>Gets or sets the final storage path when <see cref="IsComplete"/> is <c>true</c>.</summary>
    public string? StoragePath { get; set; }
}
