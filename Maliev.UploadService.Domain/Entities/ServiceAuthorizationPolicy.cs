using System.ComponentModel.DataAnnotations;

namespace Maliev.UploadService.Domain.Entities;

/// <summary>
/// Defines what a service is permitted to upload and how much storage it may consume.
/// Used for the legacy authorization path; IAM service is the primary gate.
/// </summary>
public class ServiceAuthorizationPolicy
{
    /// <summary>Gets or sets the unique policy identifier.</summary>
    [Key]
    [Required]
    public required string PolicyId { get; set; }

    /// <summary>Gets or sets the service identity this policy governs.</summary>
    [Required]
    public required string ServiceId { get; set; }

    /// <summary>Gets or sets the human-readable service name.</summary>
    [Required]
    public required string ServiceName { get; set; }

    /// <summary>Gets or sets the list of GCS path prefixes this service may write to.</summary>
    [Required]
    [MinLength(1)]
    public required List<string> AllowedPathPrefixes { get; set; }

    /// <summary>Gets or sets the list of MIME content types this service may upload.</summary>
    [Required]
    [MinLength(1)]
    public required List<string> AllowedContentTypes { get; set; }

    /// <summary>Gets or sets the maximum file size in bytes for a single upload.</summary>
    [Required]
    [Range(1, long.MaxValue)]
    public required long MaxFileSizeBytes { get; set; }

    /// <summary>Gets or sets the total storage quota in bytes for this service.</summary>
    [Required]
    [Range(0, long.MaxValue)]
    public required long StorageQuotaBytes { get; set; }

    /// <summary>Gets or sets whether this service may overwrite existing files.</summary>
    [Required]
    public bool AllowOverwrite { get; set; } = false;

    /// <summary>Gets or sets whether this service may initiate resumable uploads.</summary>
    [Required]
    public bool AllowResumableUpload { get; set; } = true;

    /// <summary>Gets or sets when this policy was created.</summary>
    [Required]
    public required DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets when this policy was last updated.</summary>
    [Required]
    public required DateTime UpdatedAt { get; set; }

    /// <summary>Gets or sets whether this policy is currently in effect.</summary>
    [Required]
    public bool IsActive { get; set; } = true;
}
