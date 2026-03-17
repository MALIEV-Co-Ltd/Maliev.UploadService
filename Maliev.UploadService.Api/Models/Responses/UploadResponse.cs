namespace Maliev.UploadService.Api.Models.Responses;

/// <summary>
/// Response model for file upload operations.
/// </summary>
public class UploadResponse
{
    /// <summary>
    /// Gets or sets the unique identifier for the upload.
    /// </summary>
    public string UploadId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the service identifier that performed the upload.
    /// </summary>
    public string ServiceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the original file name.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content type of the file.
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the size of the file in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Gets or sets the checksum of the file.
    /// </summary>
    public string? Checksum { get; set; }

    /// <summary>
    /// Gets or sets the storage path of the file.
    /// </summary>
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current status of the upload.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the file was uploaded.
    /// </summary>
    public DateTime UploadedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the upload was completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the signed URL for downloading the file.
    /// </summary>
    public string? SignedUrl { get; set; }
}
