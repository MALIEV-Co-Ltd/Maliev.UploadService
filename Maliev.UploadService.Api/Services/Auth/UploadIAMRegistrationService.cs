using Maliev.Aspire.ServiceDefaults.IAM;

namespace Maliev.UploadService.Api.Services.Auth;

/// <summary>
/// Handles registration of Upload Service permissions and predefined roles in the central IAM Service.
/// Implements Constitution Principle XIII: Aspire &amp; Service Defaults integration.
/// </summary>
public class UploadIAMRegistrationService : IAMRegistrationService
{
    public UploadIAMRegistrationService(
        IConfiguration configuration,
        ILogger<UploadIAMRegistrationService> logger)
        : base(configuration, logger, "upload")
    {
    }

    /// <inheritdoc/>
    protected override IEnumerable<PermissionRegistration> GetPermissions()
    {
        return UploadPermissions.AllWithDescriptions.Select(p => new PermissionRegistration
        {
            PermissionId = p.Key,
            Description = p.Value
        });
    }

    /// <inheritdoc/>
    protected override IEnumerable<RoleRegistration> GetPredefinedRoles()
    {
        return UploadPredefinedRoles.All.Select(r => new RoleRegistration
        {
            RoleId = r.RoleId,
            Description = r.Description,
            PermissionIds = r.Permissions.ToList(),
            IsCustom = false
        });
    }
}
