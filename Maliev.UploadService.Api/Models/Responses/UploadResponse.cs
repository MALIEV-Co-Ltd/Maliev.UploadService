namespace Maliev.UploadService.Api.Models.Responses;

public class UploadResponse
{
    public string UploadId { get; set; } = string.Empty;
    public string ServiceId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? Checksum { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? SignedUrl { get; set; }
}
