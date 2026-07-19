namespace Maliev.UploadService.Api.Models.Responses;

/// <summary>
/// Response model for artifact upload operations.
/// </summary>
public class ArtifactUploadResponse
{
    /// <summary>
    /// Gets or sets the unique identifier for the uploaded artifact.
    /// </summary>
    public Guid ArtifactId { get; set; }

    /// <summary>
    /// Gets or sets the storage path where the artifact was saved.
    /// </summary>
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the signed URL for downloading the artifact.
    /// </summary>
    public string DownloadUrl { get; set; } = string.Empty;
}
