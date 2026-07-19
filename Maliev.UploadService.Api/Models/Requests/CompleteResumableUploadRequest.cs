using System.ComponentModel.DataAnnotations;

namespace Maliev.UploadService.Api.Models.Requests;

/// <summary>
/// Request model for completing a direct-to-GCS resumable upload.
/// </summary>
public class CompleteResumableUploadRequest
{
    /// <summary>
    /// Gets or sets the optional client-provided checksum to persist instead of the GCS MD5 hash.
    /// </summary>
    [MaxLength(64)]
    public string? Checksum { get; set; }
}
