namespace Maliev.UploadService.Api.Models;

/// <summary>
/// Storage options for file uploads
/// </summary>
public class StorageOptions
{
    /// <summary>
    /// Custom bucket name for this upload
    /// </summary>
    public string? BucketName { get; set; }

    /// <summary>
    /// Custom content type for the file
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Retention period in days
    /// </summary>
    public int? RetentionDays { get; set; }

    /// <summary>
    /// Enable versioning for this file
    /// </summary>
    public bool EnableVersioning { get; set; } = false;

    /// <summary>
    /// Enable encryption for this file
    /// </summary>
    public bool EnableEncryption { get; set; } = true;
}