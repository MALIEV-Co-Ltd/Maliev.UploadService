using System.ComponentModel.DataAnnotations;

namespace Maliev.UploadService.Api.Models.Entities;

public class ServiceAuthorizationPolicy
{
    [Key]
    [Required]
    public required string PolicyId { get; set; }

    [Required]
    public required string ServiceId { get; set; }

    [Required]
    public required string ServiceName { get; set; }

    [Required]
    [MinLength(1)]
    public required List<string> AllowedPathPrefixes { get; set; }

    [Required]
    [MinLength(1)]
    public required List<string> AllowedContentTypes { get; set; }

    [Required]
    [Range(1, long.MaxValue)]
    public required long MaxFileSizeBytes { get; set; }

    [Required]
    [Range(0, long.MaxValue)]
    public required long StorageQuotaBytes { get; set; }

    [Required]
    public bool AllowOverwrite { get; set; } = false;

    [Required]
    public bool AllowResumableUpload { get; set; } = true;

    [Required]
    public required DateTime CreatedAt { get; set; }

    [Required]
    public required DateTime UpdatedAt { get; set; }

    [Required]
    public bool IsActive { get; set; } = true;
}
