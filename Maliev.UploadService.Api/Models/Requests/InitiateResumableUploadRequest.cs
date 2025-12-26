using System.ComponentModel.DataAnnotations;

namespace Maliev.UploadService.Api.Models.Requests;

/// <summary>
/// Request model for initiating a resumable upload session (FR-022)
/// </summary>
public class InitiateResumableUploadRequest
{
    [Required]
    [MaxLength(500)]
    public required string Path { get; set; }

    [Required]
    [MaxLength(100)]
    public required string ServiceName { get; set; }

    [Required]
    [MaxLength(100)]
    public required string ContentType { get; set; }

    [Required]
    [Range(1, 10L * 1024 * 1024 * 1024)] // Max 10GB
    public required long TotalSize { get; set; }

    [MaxLength(1000)]
    public string? Metadata { get; set; }

    [MaxLength(50)]
    public string? RetentionPolicyId { get; set; }
}

