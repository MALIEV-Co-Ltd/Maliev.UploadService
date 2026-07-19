namespace Maliev.UploadService.Api.Models.Responses;

/// <summary>
/// Response model for bulk delete job status (FR-033)
/// </summary>
public class BulkDeleteJobResponse
{
    /// <summary>
    /// Gets or sets the unique identifier for the bulk delete job.
    /// </summary>
    public required string JobId { get; set; }

    /// <summary>
    /// Gets or sets the current status of the job.
    /// </summary>
    public required string Status { get; set; }

    /// <summary>
    /// Gets or sets the total number of files to be deleted.
    /// </summary>
    public required int TotalFiles { get; set; }

    /// <summary>
    /// Gets or sets the number of files successfully deleted.
    /// </summary>
    public required int FilesDeleted { get; set; }

    /// <summary>
    /// Gets or sets the number of files that failed to delete.
    /// </summary>
    public required int FilesFailed { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the job was created.
    /// </summary>
    public required DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the job was completed (if applicable).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the list of error messages (if any).
    /// </summary>
    public List<string>? Errors { get; set; }
}
