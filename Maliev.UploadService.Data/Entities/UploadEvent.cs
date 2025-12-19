using System.ComponentModel.DataAnnotations;

namespace Maliev.UploadService.Data.Entities;

public class UploadEvent
{
    [Key]
    [Required]
    public required string EventId { get; set; }

    [Required]
    public required UploadEventType EventType { get; set; }

    [Required]
    public required string ServiceId { get; set; }

    public string? UserId { get; set; }

    public string? UploadId { get; set; }

    public string? FileId { get; set; }

    public string? StoragePath { get; set; }

    [Required]
    public DateTime EventTimestamp { get; set; } = DateTime.UtcNow;

    [Required]
    public required EventResult EventResult { get; set; }

    public string? ErrorDetails { get; set; }

    public string? IpAddress { get; set; }

    public Dictionary<string, string>? Metadata { get; set; }
}

public enum UploadEventType
{
    UploadInitiated,
    UploadCompleted,
    UploadFailed,
    FileRetrieved,
    FileDeleted,
    SignedUrlGenerated,
    ValidationFailed,
    AuthorizationDenied,
    BulkDeleteInitiated
}

public enum EventResult
{
    Success,
    Failure,
    Warning
}
