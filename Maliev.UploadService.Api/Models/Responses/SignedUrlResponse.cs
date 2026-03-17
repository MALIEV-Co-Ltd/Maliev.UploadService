namespace Maliev.UploadService.Api.Models.Responses;

/// <summary>
/// Response model for signed URL generation.
/// </summary>
public class SignedUrlResponse
{
    /// <summary>
    /// Gets or sets the generated signed URL.
    /// </summary>
    public string SignedUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the signed URL expires.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier for the upload.
    /// </summary>
    public string UploadId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the storage path of the file.
    /// </summary>
    public string StoragePath { get; set; } = string.Empty;
}
