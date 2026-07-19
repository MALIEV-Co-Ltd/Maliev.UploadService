namespace Maliev.UploadService.Application.Authorization;

/// <summary>
/// Provides access to predefined roles for the Upload Service.
/// </summary>
public static class UploadPredefinedRoles
{
    public const string Admin = "roles.upload.admin";
    public const string Operator = "roles.upload.operator";
    public const string Viewer = "roles.upload.viewer";

    public static readonly IReadOnlyList<(string RoleId, string Description, string[] Permissions)> All = new List<(string, string, string[])>
    {
        (
            Admin,
            "Upload Administrator with full access",
            new[]
            {
                UploadPermissions.FileUpload,
                UploadPermissions.FileDownload,
                UploadPermissions.FileDelete,
                UploadPermissions.FileList,
                UploadPermissions.FileRead,
                UploadPermissions.MetadataUpdate,
                UploadPermissions.StorageManage,
                UploadPermissions.AdminAll,
                UploadPermissions.AdminViewMetrics,
                UploadPermissions.AdminBulkDelete,
            }
        ),
        (
            Operator,
            "Upload Operator with file and metadata access",
            new[]
            {
                UploadPermissions.FileUpload,
                UploadPermissions.FileDownload,
                UploadPermissions.FileDelete,
                UploadPermissions.FileList,
                UploadPermissions.FileRead,
                UploadPermissions.MetadataUpdate,
                UploadPermissions.AdminViewMetrics,
            }
        ),
        (
            Viewer,
            "Upload Viewer with read-only access",
            new[]
            {
                UploadPermissions.FileList,
                UploadPermissions.FileRead,
            }
        ),
    };
}
