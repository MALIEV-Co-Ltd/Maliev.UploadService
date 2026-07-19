using System.ComponentModel.DataAnnotations;

namespace Maliev.UploadService.Domain.Entities;

/// <summary>
/// Represents the persistent metadata for a successfully uploaded file.
/// </summary>
public class FileMetadata
{
    /// <summary>Gets or sets the unique file identifier.</summary>
    [Key]
    [Required]
    public required string FileId { get; set; }

    /// <summary>Gets or sets the parent upload session identifier.</summary>
    [Required]
    public required string UploadId { get; set; }

    /// <summary>Gets or sets the service that owns this file.</summary>
    [Required]
    public required string ServiceId { get; set; }

    /// <summary>Gets or sets the GCS storage path.</summary>
    [Required]
    public required string StoragePath { get; set; }

    /// <summary>Gets or sets the GCS object ETag for versioning.</summary>
    [Required]
    public required string VersionETag { get; set; }

    /// <summary>Gets or sets the file size in bytes.</summary>
    [Required]
    [Range(0, long.MaxValue)]
    public required long FileSize { get; set; }

    /// <summary>Gets or sets the MIME content type.</summary>
    [Required]
    public required string ContentType { get; set; }

    /// <summary>Gets or sets the MD5 checksum (hex-encoded).</summary>
    [Required]
    public required string Checksum { get; set; }

    /// <summary>Gets or sets when the file was uploaded.</summary>
    [Required]
    public required DateTime UploadedAt { get; set; }

    /// <summary>Gets or sets when the file was last accessed.</summary>
    public DateTime? LastAccessedAt { get; set; }

    /// <summary>Gets or sets the retention policy ID applied to this file.</summary>
    public string? RetentionPolicyId { get; set; }

    /// <summary>Gets or sets the current GCS storage class (STANDARD, NEARLINE, COLDLINE, ARCHIVE).</summary>
    [Required]
    public string StorageClass { get; set; } = "STANDARD";

    /// <summary>Gets or sets when this file expires (null means indefinite retention).</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Gets or sets custom metadata key-value pairs.</summary>
    public Dictionary<string, string>? Metadata { get; set; }

    // Navigation property
    /// <summary>Gets or sets the associated retention policy.</summary>
    public RetentionPolicy? RetentionPolicy { get; set; }
}
