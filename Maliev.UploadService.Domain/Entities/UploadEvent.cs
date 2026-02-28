using System.ComponentModel.DataAnnotations;

namespace Maliev.UploadService.Domain.Entities;

/// <summary>
/// Represents an audit trail entry for file-related events.
/// </summary>
public class UploadEvent
{
    /// <summary>Gets or sets the unique event identifier.</summary>
    [Key]
    [Required]
    public required string EventId { get; set; }

    /// <summary>Gets or sets the type of event that occurred.</summary>
    [Required]
    public required UploadEventType EventType { get; set; }

    /// <summary>Gets or sets the service that triggered the event.</summary>
    [Required]
    public required string ServiceId { get; set; }

    /// <summary>Gets or sets the user who triggered the event.</summary>
    public string? UserId { get; set; }

    /// <summary>Gets or sets the upload session identifier associated with this event.</summary>
    public string? UploadId { get; set; }

    /// <summary>Gets or sets the file identifier associated with this event.</summary>
    public string? FileId { get; set; }

    /// <summary>Gets or sets the storage path of the file involved.</summary>
    public string? StoragePath { get; set; }

    /// <summary>Gets or sets when this event occurred.</summary>
    [Required]
    public DateTime EventTimestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the outcome of the event.</summary>
    [Required]
    public required EventResult EventResult { get; set; }

    /// <summary>Gets or sets error details if the event resulted in failure.</summary>
    public string? ErrorDetails { get; set; }

    /// <summary>Gets or sets the IP address of the caller.</summary>
    public string? IpAddress { get; set; }

    /// <summary>Gets or sets additional context metadata.</summary>
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// Classifies the type of file event for auditing purposes.
/// </summary>
public enum UploadEventType
{
    /// <summary>A new upload session was initiated.</summary>
    UploadInitiated,

    /// <summary>An upload completed successfully.</summary>
    UploadCompleted,

    /// <summary>An upload failed.</summary>
    UploadFailed,

    /// <summary>A file was retrieved or its metadata was read.</summary>
    FileRetrieved,

    /// <summary>A file was deleted.</summary>
    FileDeleted,

    /// <summary>A signed URL was generated for a file.</summary>
    SignedUrlGenerated,

    /// <summary>A file failed validation checks.</summary>
    ValidationFailed,

    /// <summary>A request was denied due to insufficient permissions.</summary>
    AuthorizationDenied,

    /// <summary>A bulk delete job was initiated.</summary>
    BulkDeleteInitiated
}

/// <summary>
/// Indicates whether the associated event succeeded, failed, or produced a warning.
/// </summary>
public enum EventResult
{
    /// <summary>The operation completed successfully.</summary>
    Success,

    /// <summary>The operation failed.</summary>
    Failure,

    /// <summary>The operation completed but with warnings.</summary>
    Warning
}
