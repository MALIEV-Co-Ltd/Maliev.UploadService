namespace Maliev.UploadService.Api.Models.Responses;

public class SignedUrlResponse
{
    public string SignedUrl { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string UploadId { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
}

