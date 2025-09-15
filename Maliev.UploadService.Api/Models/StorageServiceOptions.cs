namespace Maliev.UploadService.Api.Models;

/// <summary>
/// Clean configuration options for the storage service
/// Removed all legacy business category support
/// </summary>
public class StorageServiceOptions
{
    public const string SectionName = "StorageService";

    /// <summary>
    /// Default bucket name for file storage
    /// </summary>
    public string DefaultBucketName { get; set; } = "maliev-upload-service";

    /// <summary>
    /// Maximum file size in bytes (default: 100MB)
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 100 * 1024 * 1024;

    /// <summary>
    /// Default retention period in days
    /// </summary>
    public int DefaultRetentionDays { get; set; } = 365;

    /// <summary>
    /// Enable automatic file versioning
    /// </summary>
    public bool EnableVersioning { get; set; } = false;

    /// <summary>
    /// Enable server-side encryption by default
    /// </summary>
    public bool EnableEncryption { get; set; } = true;

    /// <summary>
    /// Allowed file extensions (empty = allow all)
    /// </summary>
    public string[] AllowedFileExtensions { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Blocked file extensions for security
    /// </summary>
    public string[] BlockedFileExtensions { get; set; } =
    {
        ".exe", ".scr", ".bat", ".cmd", ".com", ".pif", ".vbs", ".js", ".jar", ".app", ".deb", ".pkg", ".dmg"
    };
}