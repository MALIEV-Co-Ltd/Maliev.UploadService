namespace Maliev.UploadService.Api.Models.Responses;

/// <summary>
/// T143: Response model for resumable upload initiation (FR-022)
/// </summary>
public class InitiateResumableUploadResponse
{
    public required string UploadId { get; set; }

    public required string SessionUri { get; set; }

    public required DateTime ExpiresAt { get; set; }

    public required long TotalSize { get; set; }
}
