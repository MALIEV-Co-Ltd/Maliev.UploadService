namespace Maliev.UploadService.Api.Services.Auth;

/// <summary>
/// Defines permission constants for the Upload Service in the {service}.{resource}.{action} format.
/// </summary>
public static class UploadPermissions
{
    public const string FilesUpload = "upload.files.upload";
    public const string FilesRead = "upload.files.read";
    public const string FilesDelete = "upload.files.delete";
    public const string FilesList = "upload.files.list";
    
    public const string AdminManagePolicies = "upload.admin.manage-policies";
    public const string AdminBulkDelete = "upload.admin.bulk-delete";
    public const string AdminViewMetrics = "upload.admin.view-metrics";
    
    public const string RetentionConfigure = "upload.retention.configure";
    public const string RetentionExecute = "upload.retention.execute";
}

