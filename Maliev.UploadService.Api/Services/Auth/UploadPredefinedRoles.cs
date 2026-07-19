namespace Maliev.UploadService.Api.Services.Auth;

/// <summary>
/// Predefined roles for the Upload Service.
/// </summary>
public static class UploadPredefinedRoles
{
    /// <summary>
    /// Full access to upload service.
    /// </summary>
    public const string Admin = "roles.upload.admin";

    /// <summary>
    /// Can upload and manage own files.
    /// </summary>
    public const string Uploader = "roles.upload.uploader";

    /// <summary>
    /// Can view and download files.
    /// </summary>
    public const string Viewer = "roles.upload.viewer";

    /// <summary>
    /// Can upload, download, delete, and manage files.
    /// </summary>
    public const string Manager = "roles.upload.manager";

    /// <summary>
    /// All predefined roles with their descriptions and permissions.
    /// </summary>
    public static readonly IReadOnlyList<(string RoleId, string Description, string[] Permissions)> All = new List<(string, string, string[])>
    {
        (Admin, "Full access to upload service", new[] { UploadPermissions.AdminAll }),

        (Uploader, "Can upload and manage own files", new[]
        {
            UploadPermissions.FilesUpload,
            UploadPermissions.FilesList,
            UploadPermissions.FilesRead
        }),

        (Viewer, "Can view and download files", new[]
        {
            UploadPermissions.FilesDownload,
            UploadPermissions.FilesList,
            UploadPermissions.FilesRead
        }),

        (Manager, "Can upload, download, delete, and manage files", new[]
        {
            UploadPermissions.FilesUpload,
            UploadPermissions.FilesDownload,
            UploadPermissions.FilesDelete,
            UploadPermissions.FilesList,
            UploadPermissions.FilesRead,
            UploadPermissions.MetadataUpdate
        })
    };
}
