using System.ComponentModel.DataAnnotations;

namespace Maliev.UploadService.Api.Models.Requests;

/// <summary>
/// Request model for uploading a processed artifact (GLB, thumbnail, preview) to GCS.
/// This endpoint is designed for internal service-to-service communication.
/// </summary>
public class UploadArtifactRequest
{
    /// <summary>
    /// Gets or sets the unique identifier for this artifact.
    /// </summary>
    [Required]
    public required Guid ArtifactId { get; set; }

    /// <summary>
    /// Gets or sets the original upload ID this artifact belongs to.
    /// </summary>
    [Required]
    public required Guid ParentUploadId { get; set; }

    /// <summary>
    /// Gets or sets the target path in GCS (e.g., projects/{projectId}/{filename}_viewer.glb).
    /// </summary>
    [Required]
    [MaxLength(500)]
    public required string StoragePath { get; set; }

    /// <summary>
    /// Gets or sets the MIME type of the artifact (e.g., model/gltf-binary, image/png).
    /// </summary>
    [Required]
    [MaxLength(100)]
    public required string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the Base64-encoded binary data of the artifact.
    /// </summary>
    [Required]
    public required string ArtifactData { get; set; }
}
