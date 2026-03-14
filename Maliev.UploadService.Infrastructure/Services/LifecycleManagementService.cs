using Maliev.UploadService.Application.Interfaces;
using Maliev.UploadService.Domain.Entities;
using Maliev.UploadService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Maliev.UploadService.Infrastructure.Services;

/// <summary>
/// Manages file lifecycle: retention policy application, expiry processing, and storage class transitions.
/// </summary>
public class LifecycleManagementService : ILifecycleManagementService
{
    private readonly UploadDbContext _context;
    private readonly IStorageService _storageService;
    private readonly ILogger<LifecycleManagementService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="LifecycleManagementService"/>.
    /// </summary>
    public LifecycleManagementService(
        UploadDbContext context,
        IStorageService storageService,
        ILogger<LifecycleManagementService> logger)
    {
        _context = context;
        _storageService = storageService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<DateTime?> ApplyRetentionPolicyAsync(
        FileMetadata fileMetadata,
        string policyId,
        CancellationToken cancellationToken = default)
    {
        var policy = await _context.RetentionPolicies
            .FirstOrDefaultAsync(p => p.PolicyId == policyId && p.IsActive, cancellationToken);

        if (policy == null)
        {
            _logger.LogWarning("Retention policy {PolicyId} not found or inactive", policyId);
            return null;
        }

        if (policy.RetentionDays == 0)
        {
            _logger.LogInformation("Applying indefinite retention policy {PolicyName} to file {FileId}",
                policy.PolicyName, fileMetadata.FileId);
            return null;
        }

        var expiresAt = fileMetadata.UploadedAt.AddDays(policy.RetentionDays);

        _logger.LogInformation(
            "Applied retention policy {PolicyName} ({RetentionDays} days) to file {FileId}. Expires at {ExpiresAt}",
            policy.PolicyName, policy.RetentionDays, fileMetadata.FileId, expiresAt);

        return expiresAt;
    }

    /// <inheritdoc/>
    public async Task<RetentionPolicy?> GetActiveRetentionPolicyAsync(
        string serviceId,
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        var query = _context.RetentionPolicies
            .Where(p => p.IsActive && (p.ServiceId == serviceId || p.ServiceId == null))
            .OrderByDescending(p => p.ServiceId != null)
            .ThenByDescending(p => p.ApplyToPathPrefix!.Length);

        var policies = await query.ToListAsync(cancellationToken);

        foreach (var policy in policies)
        {
            if (string.IsNullOrEmpty(policy.ApplyToPathPrefix))
            {
                return policy;
            }

            if (storagePath.StartsWith(policy.ApplyToPathPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return policy;
            }
        }

        _logger.LogDebug("No active retention policy found for service {ServiceId} and path {StoragePath}",
            serviceId, storagePath);

        return null;
    }

    /// <inheritdoc/>
    public string GetStorageClassForAge(int ageInDays, List<StorageClassTransition>? transitions)
    {
        if (transitions == null || transitions.Count == 0)
        {
            return "STANDARD";
        }

        var sortedTransitions = transitions.OrderBy(t => t.Days).ToList();
        var storageClass = "STANDARD";

        foreach (var transition in sortedTransitions)
        {
            if (ageInDays >= transition.Days)
            {
                storageClass = transition.StorageClass;
            }
            else
            {
                break;
            }
        }

        return storageClass;
    }

    /// <inheritdoc/>
    public async Task<int> ProcessExpiredFilesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var expiredFiles = await _context.FileMetadata
            .Where(f => f.ExpiresAt != null && f.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        if (expiredFiles.Count == 0)
        {
            _logger.LogDebug("No expired files found for processing");
            return 0;
        }

        _logger.LogInformation("Found {Count} expired files for processing", expiredFiles.Count);

        foreach (var file in expiredFiles)
        {
            _logger.LogInformation(
                "File {FileId} at path {StoragePath} expired on {ExpiresAt}. Deleting from GCS and database.",
                file.FileId, file.StoragePath, file.ExpiresAt);

            try
            {
                await _storageService.DeleteFileAsync(file.StoragePath, cancellationToken);
                _context.FileMetadata.Remove(file);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete expired file {FileId} at {StoragePath}", file.FileId, file.StoragePath);
                continue;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return expiredFiles.Count;
    }

    /// <inheritdoc/>
    public async Task<int> UpdateStorageClassesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var updatedCount = 0;

        var filesWithPolicies = await _context.FileMetadata
            .Where(f => f.RetentionPolicyId != null)
            .Include(f => f.RetentionPolicy)
            .ToListAsync(cancellationToken);

        foreach (var file in filesWithPolicies)
        {
            if (file.RetentionPolicy == null ||
                file.RetentionPolicy.StorageClassTransitions == null ||
                file.RetentionPolicy.StorageClassTransitions.Count == 0)
            {
                continue;
            }

            var ageInDays = (int)(now - file.UploadedAt).TotalDays;
            var targetStorageClass = GetStorageClassForAge(ageInDays, file.RetentionPolicy.StorageClassTransitions);

            if (file.StorageClass != targetStorageClass)
            {
                _logger.LogInformation(
                    "File {FileId} ({AgeInDays} days old) should transition from {CurrentClass} to {TargetClass}",
                    file.FileId, ageInDays, file.StorageClass, targetStorageClass);

                try
                {
                    await _storageService.UpdateStorageClassAsync(file.StoragePath, targetStorageClass, cancellationToken);
                    file.StorageClass = targetStorageClass;
                    updatedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to update storage class for file {FileId} to {TargetClass}", file.FileId, targetStorageClass);
                }
            }
        }

        if (updatedCount > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Updated storage class for {Count} files", updatedCount);
        }

        return updatedCount;
    }
}
