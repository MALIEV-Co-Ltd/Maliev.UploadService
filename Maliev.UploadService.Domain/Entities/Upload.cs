using System.ComponentModel.DataAnnotations;

namespace Maliev.UploadService.Domain.Entities;

/// <summary>
/// Represents a file upload session and its current state.
/// </summary>
public class Upload
{
    /// <summary>Gets or sets the unique upload identifier.</summary>
    [Key]
    [Required]
    public required string UploadId { get; set; }

    /// <summary>Gets or sets the service that initiated the upload.</summary>
    [Required]
    public required string ServiceId { get; set; }

    /// <summary>Gets or sets the optional user who initiated the upload.</summary>
    public string? UserId { get; set; }

    /// <summary>Gets or sets the original file name.</summary>
    [Required]
    public required string FileName { get; set; }

    /// <summary>Gets or sets the MIME content type of the file.</summary>
    [Required]
    public required string ContentType { get; set; }

    /// <summary>Gets or sets the total size of the file in bytes.</summary>
    [Required]
    [Range(0, long.MaxValue)]
    public required long FileSize { get; set; }

    /// <summary>Gets or sets the file checksum (MD5 or SHA256 hex).</summary>
    public string? Checksum { get; set; }

    /// <summary>Gets or sets the storage path in GCS.</summary>
    [Required]
    public required string StoragePath { get; set; }

    /// <summary>Gets or sets the GCS resumable upload session URI.</summary>
    public string? SessionUri { get; set; }

    /// <summary>Gets or sets the number of bytes successfully uploaded so far.</summary>
    [Required]
    [Range(0, long.MaxValue)]
    public long BytesUploaded { get; set; } = 0;

    /// <summary>Gets or sets the current upload status.</summary>
    [Required]
    public required UploadStatus Status { get; set; }

    /// <summary>Gets or sets when the upload was initiated.</summary>
    [Required]
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets when the upload completed successfully.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Gets or sets the error message if the upload failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Gets or sets the retention policy ID applied to this upload.</summary>
    public string? RetentionPolicyId { get; set; }

    /// <summary>Gets or sets custom metadata key-value pairs.</summary>
    public Dictionary<string, string>? Metadata { get; set; }
}
