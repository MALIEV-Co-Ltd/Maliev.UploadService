namespace Maliev.UploadService.Api.Models.Responses;

public class FileMetadataResponse
{
    public string FileId { get; set; } = string.Empty;
    public string UploadId { get; set; } = string.Empty;
    public string ServiceId { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string VersionETag { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string Checksum { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public DateTime? LastAccessedAt { get; set; }
    public string? StorageClass { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

