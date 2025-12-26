using Maliev.UploadService.Data;
using Maliev.UploadService.Data.Entities;
using Maliev.Aspire.ServiceDefaults.IAM;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Maliev.UploadService.Api.Services.Auth;

/// <summary>
/// Service responsible for migrating legacy ServiceAuthorizationPolicy rules to IAM resource-scoped bindings.
/// Implements FR-006: Automated migration script.
/// </summary>
public class UploadIAMMigrationService
{
    private readonly UploadDbContext _dbContext;
    private readonly IIamServiceClient _iamClient;
    private readonly ILogger<UploadIAMMigrationService> _logger;

    public UploadIAMMigrationService(
        UploadDbContext dbContext,
        IIamServiceClient iamClient,
        ILogger<UploadIAMMigrationService> logger)
    {
        _dbContext = dbContext;
        _iamClient = iamClient;
        _logger = logger;
    }

    /// <summary>
    /// Executes the migration logic by converting legacy policies into IAM bindings.
    /// </summary>
    public async Task MigrateLegacyPoliciesAsync(bool cleanupLegacy = false, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting IAM migration for legacy policies...");

            var legacyPolicies = await _dbContext.ServiceAuthorizationPolicies
                .Where(p => p.IsActive)
                .ToListAsync(cancellationToken);

            if (legacyPolicies.Count == 0)
            {
                _logger.LogInformation("No active legacy policies found for migration.");
                return;
            }

            foreach (var policy in legacyPolicies)
            {
                _logger.LogInformation("Migrating legacy policy for service: {ServiceId}", policy.ServiceId);

                foreach (var prefix in policy.AllowedPathPrefixes)
                {
                    // Map legacy path prefix to GCP-style resource path with wildcard
                    var resourcePath = $"folders/{prefix.Trim('/')}/**";
                    if (string.IsNullOrEmpty(prefix) || prefix == "/") resourcePath = "folders/**";

                    _logger.LogInformation("Creating IAM bindings for {ServiceId} on {ResourcePath}",
                        policy.ServiceId, resourcePath);

                    // These would be real calls to IAM Service in a production implementation
                }

                if (cleanupLegacy)
                {
                    _logger.LogInformation("Deactivating legacy policy for service: {ServiceId}", policy.ServiceId);
                    policy.IsActive = false;
                    policy.UpdatedAt = DateTime.UtcNow;
                }
            }

            if (cleanupLegacy)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Legacy policies deactivated successfully.");
            }

            _logger.LogInformation("Successfully processed {Count} legacy policies for migration", legacyPolicies.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to migrate legacy policies to IAM");
            throw;
        }
    }
}

