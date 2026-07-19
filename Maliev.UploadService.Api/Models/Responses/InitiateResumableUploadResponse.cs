namespace Maliev.UploadService.Api.Models.Responses;

/// <summary>
/// Response model for resumable upload initiation (FR-022)
/// </summary>
public class InitiateResumableUploadResponse
{
    /// <summary>
    /// Gets or sets the unique identifier for the upload session.
    /// </summary>
    public required string UploadId { get; set; }

    /// <summary>
    /// Gets or sets the URI for the resumable upload session.
    /// </summary>
    public required string SessionUri { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the session expires.
    /// </summary>
    public required DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the total size of the file being uploaded.
    /// </summary>
    public required long TotalSize { get; set; }
}
