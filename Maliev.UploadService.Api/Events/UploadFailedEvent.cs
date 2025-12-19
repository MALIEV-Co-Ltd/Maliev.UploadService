namespace Maliev.UploadService.Api.Events;

/// <summary>
/// Event published when an upload fails (FR-025)
/// Routing key: maliev.uploadservice.v1.upload.failed
/// </summary>
public class UploadFailedEvent
{
    public required string UploadId { get; set; }

    public required string ServiceId { get; set; }

    public required string StoragePath { get; set; }

    public required string FileName { get; set; }

    public required DateTime FailedAt { get; set; }

    public required string ErrorMessage { get; set; }

    public string? ErrorDetails { get; set; }
}
