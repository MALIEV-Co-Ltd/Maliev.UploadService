namespace Maliev.UploadService.Api.Models.Responses;

/// <summary>
/// Response model for resumable upload continuation (FR-022)
/// </summary>
public class ResumeUploadResponse
{
    /// <summary>
    /// Gets or sets the unique identifier for the upload session.
    /// </summary>
    public required string UploadId { get; set; }

    /// <summary>
    /// Gets or sets the number of bytes received so far.
    /// </summary>
    public required long BytesReceived { get; set; }

    /// <summary>
    /// Gets or sets the total size of the file.
    /// </summary>
    public required long TotalSize { get; set; }

    /// <summary>
    /// Gets or sets whether the upload is complete.
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    /// Gets or sets the storage path of the uploaded file.
    /// Only populated when IsComplete is true.
    /// </summary>
    public string? StoragePath { get; set; }

    /// <summary>
    /// Gets or sets the next byte range to upload (e.g., "1048576-2097151").
    /// Only populated when IsComplete is false.
    /// </summary>
    public string? NextByteRange { get; set; }
}
