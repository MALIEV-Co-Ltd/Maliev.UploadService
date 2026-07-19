using System.ComponentModel.DataAnnotations;

namespace Maliev.UploadService.Api.Models.Requests;

/// <summary>
/// Request model for generating a signed URL by GCS storage path.
/// Used by internal services (e.g. IntranetBff consumers) that have the storage path
/// but not the uploadId, and are already authenticated via service-account tokens.
/// </summary>
public class GenerateSignedUrlByPathRequest
{
    /// <summary>
    /// The GCS storage path of the file (e.g. "projects/.../file.stl_preview_bottom.png").
    /// </summary>
    [Required]
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the expiration time in minutes (default: 60, max: 10080 = 7 days).
    /// </summary>
    [Range(1, 10080)]
    public int ExpirationMinutes { get; set; } = 60;
}
