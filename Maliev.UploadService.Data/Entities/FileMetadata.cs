using System.ComponentModel.DataAnnotations;

namespace Maliev.UploadService.Data.Entities;

public class FileMetadata
{
    [Key]
    [Required]
    public required string FileId { get; set; }

    [Required]
    public required string UploadId { get; set; }

    [Required]
    public required string ServiceId { get; set; }

    [Required]
    public required string StoragePath { get; set; }

    [Required]
    public required string VersionETag { get; set; }

    [Required]
    [Range(0, long.MaxValue)]
    public required long FileSize { get; set; }

    [Required]
    public required string ContentType { get; set; }

    [Required]
    public required string Checksum { get; set; }

    [Required]
    public required DateTime UploadedAt { get; set; }

    public DateTime? LastAccessedAt { get; set; }

    public string? RetentionPolicyId { get; set; }

    [Required]
    public string StorageClass { get; set; } = "STANDARD";

    public DateTime? ExpiresAt { get; set; }

    public Dictionary<string, string>? Metadata { get; set; }

    // Navigation properties
    public RetentionPolicy? RetentionPolicy { get; set; }
}

