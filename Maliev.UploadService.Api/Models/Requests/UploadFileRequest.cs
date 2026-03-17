using System.ComponentModel.DataAnnotations;

namespace Maliev.UploadService.Api.Models.Requests;

/// <summary>
/// Request model for uploading a file.
/// </summary>
public class UploadFileRequest
{
    /// <summary>
    /// Gets or sets the file to upload.
    /// </summary>
    [Required]
    public required IFormFile File { get; set; }

    /// <summary>
    /// Gets or sets the storage path where the file will be saved.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public required string Path { get; set; }

    /// <summary>
    /// Gets or sets the name of the service performing the upload.
    /// </summary>
    [MaxLength(100)]
    public string? ServiceName { get; set; }

    /// <summary>
    /// Gets or sets whether to overwrite an existing file at the same path.
    /// </summary>
    public bool Overwrite { get; set; } = false;

    /// <summary>
    /// Gets or sets optional metadata for the file.
    /// </summary>
    [MaxLength(1000)]
    public string? Metadata { get; set; }

    /// <summary>
    /// Gets or sets the optional retention policy ID to apply to the uploaded file.
    /// </summary>
    [MaxLength(50)]
    public string? RetentionPolicyId { get; set; }
}
