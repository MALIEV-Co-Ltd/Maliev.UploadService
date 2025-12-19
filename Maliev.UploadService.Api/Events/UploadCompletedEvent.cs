namespace Maliev.UploadService.Api.Events;

/// <summary>
/// Event published when a file upload completes successfully
/// Routing Key: maliev.uploadservice.v1.upload.completed
/// </summary>
public class UploadCompletedEvent
{
    public required string UploadId { get; set; }
    public required string ServiceId { get; set; }
    public required string FileName { get; set; }
    public required string StoragePath { get; set; }
    public required string ContentType { get; set; }
    public required long FileSize { get; set; }
    public required DateTime UploadedAt { get; set; }
    public string? RetentionPolicyId { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}
