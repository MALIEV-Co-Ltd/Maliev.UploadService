namespace Maliev.UploadService.Api.Models.Responses;

/// <summary>
/// Response model containing file metadata information.
/// </summary>
public class FileMetadataResponse
{
    /// <summary>
    /// Gets or sets the unique identifier for the file.
    /// </summary>
    public string FileId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unique identifier for the upload.
    /// </summary>
    public string UploadId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the service identifier that owns the file.
    /// </summary>
    public string ServiceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the storage path of the file.
    /// </summary>
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the version ETag of the file.
    /// </summary>
    public string VersionETag { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the size of the file in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Gets or sets the content type of the file.
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the checksum of the file.
    /// </summary>
    public string Checksum { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the file was uploaded.
    /// </summary>
    public DateTime UploadedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the file was last accessed.
    /// </summary>
    public DateTime? LastAccessedAt { get; set; }

    /// <summary>
    /// Gets or sets the storage class of the file.
    /// </summary>
    public string? StorageClass { get; set; }

    /// <summary>
    /// Gets or sets the expiration timestamp for the file.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the custom metadata for the file.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }
}
