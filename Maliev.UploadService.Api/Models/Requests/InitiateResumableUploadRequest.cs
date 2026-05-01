using System.ComponentModel.DataAnnotations;

namespace Maliev.UploadService.Api.Models.Requests;

/// <summary>
/// Request model for initiating a resumable upload session (FR-022)
/// </summary>
public class InitiateResumableUploadRequest
{
    /// <summary>
    /// Gets or sets the storage path where the file will be uploaded.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public required string Path { get; set; }

    /// <summary>
    /// Gets or sets the original file name.
    /// </summary>
    [Required]
    [MaxLength(255)]
    public required string FileName { get; set; }

    /// <summary>
    /// Gets or sets the name of the service performing the upload.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public required string ServiceName { get; set; }

    /// <summary>
    /// Gets or sets the content type of the file.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public required string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the total size of the file in bytes.
    /// </summary>
    [Required]
    [Range(1, 10L * 1024 * 1024 * 1024)] // Max 10GB
    public required long TotalSize { get; set; }

    /// <summary>
    /// Gets or sets optional metadata for the file.
    /// </summary>
    [MaxLength(1000)]
    public string? Metadata { get; set; }

    /// <summary>
    /// Gets or sets the retention policy ID to apply to the uploaded file.
    /// </summary>
    [MaxLength(50)]
    public string? RetentionPolicyId { get; set; }

    /// <summary>
    /// Gets or sets whether to overwrite an existing file at the same path.
    /// </summary>
    public bool Overwrite { get; set; }

    /// <summary>
    /// Gets or sets the optional client-provided checksum (MD5/SHA256).
    /// </summary>
    [MaxLength(64)]
    public string? Checksum { get; set; }
}
