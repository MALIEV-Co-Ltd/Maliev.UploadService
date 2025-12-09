namespace Maliev.UploadService.Api.Models.Responses;

/// <summary>
/// T144: Response model for resumable upload continuation (FR-022)
/// </summary>
public class ResumeUploadResponse
{
    public required string UploadId { get; set; }

    public required long BytesReceived { get; set; }

    public required long TotalSize { get; set; }

    public bool IsComplete { get; set; }

    /// <summary>
    /// Only populated when IsComplete is true
    /// </summary>
    public string? StoragePath { get; set; }

    /// <summary>
    /// Next byte range to upload (e.g., "1048576-2097151")
    /// Only populated when IsComplete is false
    /// </summary>
    public string? NextByteRange { get; set; }
}
