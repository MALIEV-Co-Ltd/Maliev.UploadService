using System.ComponentModel.DataAnnotations;

namespace Maliev.UploadService.Domain.Entities;

/// <summary>
/// Represents a bulk file deletion job and tracks its progress.
/// </summary>
public class BulkDeleteJob
{
    /// <summary>Gets or sets the unique job identifier.</summary>
    [Key]
    [Required]
    public required string JobId { get; set; }

    /// <summary>Gets or sets the service that owns the files to be deleted.</summary>
    [Required]
    public required string ServiceId { get; set; }

    /// <summary>Gets or sets the path prefix filter for files to delete.</summary>
    [Required]
    public string PathPrefix { get; set; } = string.Empty;

    /// <summary>Gets or sets who initiated the bulk delete job.</summary>
    [Required]
    public required string InitiatedBy { get; set; }

    /// <summary>Gets or sets the current job status.</summary>
    [Required]
    public required BulkDeleteStatus Status { get; set; }

    /// <summary>Gets or sets the total number of files to be deleted.</summary>
    [Required]
    [Range(0, int.MaxValue)]
    public int FilesTotal { get; set; } = 0;

    /// <summary>Gets or sets the number of files processed so far.</summary>
    [Required]
    [Range(0, int.MaxValue)]
    public int FilesProcessed { get; set; } = 0;

    /// <summary>Gets or sets the number of files successfully deleted.</summary>
    [Required]
    [Range(0, int.MaxValue)]
    public int FilesDeleted { get; set; } = 0;

    /// <summary>Gets or sets the number of files that failed to delete.</summary>
    [Required]
    [Range(0, int.MaxValue)]
    public int ErrorCount { get; set; } = 0;

    /// <summary>Gets or sets an optional list of specific upload IDs to delete.</summary>
    public List<string>? UploadIds { get; set; }

    /// <summary>Gets or sets a cutoff date — only files older than this date will be deleted.</summary>
    public DateTime? DeleteFilesOlderThan { get; set; }

    /// <summary>Gets or sets the reason for the bulk deletion.</summary>
    public string? Reason { get; set; }

    /// <summary>Gets or sets when the job was created.</summary>
    [Required]
    public required DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets when processing started.</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>Gets or sets when the job completed.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Gets or sets error details for files that failed to delete.</summary>
    public List<string>? ErrorDetails { get; set; }

    // Alias properties for backward compatibility with service layer

    /// <summary>Gets or sets total file count (alias for <see cref="FilesTotal"/>).</summary>
    public int TotalFiles
    {
        get => FilesTotal;
        set => FilesTotal = value;
    }

    /// <summary>Gets or sets failed file count (alias for <see cref="ErrorCount"/>).</summary>
    public int FilesFailed
    {
        get => ErrorCount;
        set => ErrorCount = value;
    }

    /// <summary>Gets or sets error list (alias for <see cref="ErrorDetails"/>).</summary>
    public List<string>? Errors
    {
        get => ErrorDetails;
        set => ErrorDetails = value;
    }
}

/// <summary>
/// Represents the processing state of a bulk delete job.
/// </summary>
public enum BulkDeleteStatus
{
    /// <summary>Job is queued and awaiting processing.</summary>
    Queued,

    /// <summary>Job is currently being processed.</summary>
    InProgress,

    /// <summary>Job completed successfully with all files deleted.</summary>
    Completed,

    /// <summary>Job completed but some files could not be deleted.</summary>
    CompletedWithErrors,

    /// <summary>Job failed entirely.</summary>
    Failed,

    /// <summary>Job was cancelled before completion.</summary>
    Cancelled
}
