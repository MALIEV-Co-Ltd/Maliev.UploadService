namespace Maliev.UploadService.Api.Services.Auth;

/// <summary>
/// Constants for Upload Service permissions.
/// Follows GCP-style naming: {service}.{resource}.{action}
/// </summary>
public static class UploadPermissions
{
    /// <summary>
    /// Permission to upload files to storage.
    /// </summary>
    public const string FilesUpload = "upload.files.upload";

    /// <summary>
    /// Permission to download files from storage.
    /// </summary>
    public const string FilesDownload = "upload.files.download";

    /// <summary>
    /// Permission to delete files from storage.
    /// </summary>
    public const string FilesDelete = "upload.files.delete";

    /// <summary>
    /// Permission to list uploaded files.
    /// </summary>
    public const string FilesList = "upload.files.list";

    /// <summary>
    /// Permission to read file metadata.
    /// </summary>
    public const string FilesRead = "upload.files.read";

    /// <summary>
    /// Permission to update file metadata.
    /// </summary>
    public const string MetadataUpdate = "upload.metadata.update";

    /// <summary>
    /// Permission to manage storage settings.
    /// </summary>
    public const string StorageManage = "upload.storage.manage";

    /// <summary>
    /// Full administrative access to upload service.
    /// </summary>
    public const string AdminAll = "upload.admin.all";

    /// <summary>
    /// Permission to view upload metrics.
    /// </summary>
    public const string AdminViewMetrics = "upload.admin.view-metrics";

    /// <summary>
    /// Permission to perform bulk delete operations.
    /// </summary>
    public const string AdminBulkDelete = "upload.admin.bulk-delete";

    /// <summary>
    /// Collection of all defined upload permissions with descriptions.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> AllWithDescriptions = new Dictionary<string, string>
    {
        { FilesUpload, "Upload files to storage" },
        { FilesDownload, "Download files from storage" },
        { FilesDelete, "Delete files from storage" },
        { FilesList, "List uploaded files" },
        { FilesRead, "Read file metadata" },
        { MetadataUpdate, "Update file metadata" },
        { StorageManage, "Manage storage settings" },
        { AdminAll, "Full administrative access to upload service" },
        { AdminViewMetrics, "View upload service metrics and usage" },
        { AdminBulkDelete, "Perform bulk file deletion operations" }
    };

    /// <summary>All available permission codes</summary>
    public static IEnumerable<string> All => AllWithDescriptions.Keys;
}
