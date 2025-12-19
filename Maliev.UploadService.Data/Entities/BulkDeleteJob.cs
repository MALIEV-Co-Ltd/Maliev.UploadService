using System.ComponentModel.DataAnnotations;

namespace Maliev.UploadService.Data.Entities;

public class BulkDeleteJob
{
    [Key]
    [Required]
    public required string JobId { get; set; }

    [Required]
    public required string ServiceId { get; set; }

    [Required]
    public string PathPrefix { get; set; } = string.Empty;

    [Required]
    public required string InitiatedBy { get; set; }

    [Required]
    public required BulkDeleteStatus Status { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int FilesTotal { get; set; } = 0;

    [Required]
    [Range(0, int.MaxValue)]
    public int FilesProcessed { get; set; } = 0;

    [Required]
    [Range(0, int.MaxValue)]
    public int FilesDeleted { get; set; } = 0;

    [Required]
    [Range(0, int.MaxValue)]
    public int ErrorCount { get; set; } = 0;

    // Additional filtering options
    public List<string>? UploadIds { get; set; }

    public DateTime? DeleteFilesOlderThan { get; set; }

    public string? Reason { get; set; }

    [Required]
    public required DateTime CreatedAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public List<string>? ErrorDetails { get; set; }

    // Alias properties for backward compatibility with service layer
    public int TotalFiles
    {
        get => FilesTotal;
        set => FilesTotal = value;
    }

    public int FilesFailed
    {
        get => ErrorCount;
        set => ErrorCount = value;
    }

    public List<string>? Errors
    {
        get => ErrorDetails;
        set => ErrorDetails = value;
    }
}

public enum BulkDeleteStatus
{
    Queued,
    InProgress,
    Completed,
    CompletedWithErrors,
    Failed,
    Cancelled
}
