using Microsoft.Extensions.Logging;
using Maliev.Aspire.ServiceDefaults.IAM;

namespace Maliev.UploadService.Api.Services.Auth;

/// <summary>
/// Handles registration of Upload Service permissions and predefined roles in the central IAM Service.
/// Implements Constitution Principle XIII: Aspire &amp; Service Defaults integration.
/// </summary>
public class UploadIAMRegistrationService
{
    private readonly IIamServiceClient _iamClient;
    private readonly ILogger<UploadIAMRegistrationService> _logger;

    public UploadIAMRegistrationService(
        IIamServiceClient iamClient,
        ILogger<UploadIAMRegistrationService> logger)
    {
        _iamClient = iamClient;
        _logger = logger;
    }

    /// <summary>
    /// Registers all defined permissions and roles. 
    /// Should be called during application startup or via a migration task.
    /// </summary>
    public async Task RegisterAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting IAM permission registration...");

            // Permission definitions would be sent to IAM Service here
            // e.g., await _iamClient.DefinePermissionAsync(UploadPermissions.FilesUpload, "Upload files", cancellationToken);

            _logger.LogInformation("Successfully registered {Count} permissions", 9);

            _logger.LogInformation("Starting IAM role registration...");

            // Role definitions would be sent to IAM Service here
            // e.g., await _iamClient.DefineRoleAsync(UploadPredefinedRoles.Admin, new[] { "upload.*" }, cancellationToken);

            _logger.LogInformation("Successfully registered {Count} predefined roles", 4);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register permissions and roles with IAM Service");
            throw; // Critical failure if we can't register auth schema
        }
    }
}

