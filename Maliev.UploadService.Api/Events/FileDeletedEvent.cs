namespace Maliev.UploadService.Api.Events;

/// <summary>
/// T157: Event published when a file is deleted (FR-025)
/// Routing key: maliev.uploadservice.v1.file.deleted
/// </summary>
public class FileDeletedEvent
{
    public required string FileId { get; set; }

    public required string UploadId { get; set; }

    public required string ServiceId { get; set; }

    public required string StoragePath { get; set; }

    public required DateTime DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    public string? Reason { get; set; }
}
