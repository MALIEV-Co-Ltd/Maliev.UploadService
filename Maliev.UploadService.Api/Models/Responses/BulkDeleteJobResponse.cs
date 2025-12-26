namespace Maliev.UploadService.Api.Models.Responses;

/// <summary>
/// Response model for bulk delete job status (FR-033)
/// </summary>
public class BulkDeleteJobResponse
{
    public required string JobId { get; set; }

    public required string Status { get; set; }

    public required int TotalFiles { get; set; }

    public required int FilesDeleted { get; set; }

    public required int FilesFailed { get; set; }

    public required DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public List<string>? Errors { get; set; }
}

