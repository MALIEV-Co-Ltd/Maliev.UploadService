namespace Maliev.UploadService.Api.Models;

public class FileUploadResponse
{
    public Guid FileId { get; set; }
    public string ObjectName { get; set; } = string.Empty;
    public string Bucket { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public string Category { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? Subcategory { get; set; }
    public AccessLevel AccessLevel { get; set; }
    public ProcessingStatus ProcessingStatus { get; set; }
    public string[]? Tags { get; set; }
}

public enum ProcessingStatus
{
    Uploading,
    Processing,
    Completed,
    Failed,
    Quarantine
}