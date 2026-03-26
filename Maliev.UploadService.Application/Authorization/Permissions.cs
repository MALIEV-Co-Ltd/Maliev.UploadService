namespace Maliev.UploadService.Application.Authorization;

/// <summary>
/// Defines the permissions for the Upload Service.
/// </summary>
public static class UploadPermissions
{
    public const string FileUpload = "upload.files.upload";
    public const string FileDownload = "upload.files.download";
    public const string FileDelete = "upload.files.delete";
    public const string FileList = "upload.files.list";
    public const string FileRead = "upload.files.read";

    public const string MetadataUpdate = "upload.metadata.update";

    public const string StorageManage = "upload.storage.manage";

    public const string AdminAll = "upload.admin.all";
    public const string AdminViewMetrics = "upload.admin.view-metrics";
    public const string AdminBulkDelete = "upload.admin.bulk-delete";

    public static readonly IReadOnlyDictionary<string, string> AllWithDescriptions = new Dictionary<string, string>
    {
        { FileUpload, "Upload files" },
        { FileDownload, "Download files" },
        { FileDelete, "Delete files" },
        { FileList, "List files" },
        { FileRead, "Read file metadata" },
        { MetadataUpdate, "Update file metadata" },
        { StorageManage, "Manage storage" },
        { AdminAll, "Full upload admin access" },
        { AdminViewMetrics, "View upload metrics" },
        { AdminBulkDelete, "Bulk delete files" },
    };

    public static string[] All => AllWithDescriptions.Keys.ToArray();
}
