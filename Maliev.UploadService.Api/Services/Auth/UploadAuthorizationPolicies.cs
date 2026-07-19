namespace Maliev.UploadService.Api.Services.Auth;

/// <summary>Names authorization policies owned by UploadService.</summary>
public static class UploadAuthorizationPolicies
{
    /// <summary>
    /// Requires an authenticated JWT subject without granting any operation permission.
    /// Scoped operations perform their authoritative permission check after resolving a sanitized storage path.
    /// </summary>
    public const string AuthenticatedSubject = "upload.authenticated-subject";
}
