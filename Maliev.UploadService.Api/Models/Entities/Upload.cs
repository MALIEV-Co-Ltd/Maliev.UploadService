using System.ComponentModel.DataAnnotations;

namespace Maliev.UploadService.Api.Models.Entities;

public class Upload
{
    [Key]
    [Required]
    public required string UploadId { get; set; }

    [Required]
    public required string ServiceId { get; set; }

    public string? UserId { get; set; }

    [Required]
    public required string FileName { get; set; }

    [Required]
    public required string ContentType { get; set; }

    [Required]
    [Range(0, long.MaxValue)]
    public required long FileSize { get; set; }

    public string? Checksum { get; set; }

    [Required]
    public required string StoragePath { get; set; }

    public string? SessionUri { get; set; }

    [Required]
    [Range(0, long.MaxValue)]
    public long BytesUploaded { get; set; } = 0;

    [Required]
    public required UploadStatus Status { get; set; }

    [Required]
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public string? ErrorMessage { get; set; }

    public string? RetentionPolicyId { get; set; }

    public Dictionary<string, string>? Metadata { get; set; }
}

public enum UploadStatus
{
    Pending,
    InProgress,
    Validating,
    Completed,
    Failed
}
