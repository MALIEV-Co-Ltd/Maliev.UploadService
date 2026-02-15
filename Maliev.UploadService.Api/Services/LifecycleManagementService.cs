using Maliev.UploadService.Data;
using Maliev.UploadService.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Maliev.UploadService.Api.Services;

/// <summary>
/// Service for managing file lifecycle and retention policies with GCS integration
/// </summary>
public class LifecycleManagementService : ILifecycleManagementService
{
    private readonly UploadDbContext _context;
    private readonly IStorageService _storageService;
    private readonly ILogger<LifecycleManagementService> _logger;

    public LifecycleManagementService(
        UploadDbContext context,
        IStorageService storageService,
        ILogger<LifecycleManagementService> logger)
    {
        _context = context;
        _storageService = storageService;
        _logger = logger;
    }

    /// <summary>
    /// Applies a retention policy to a file and calculates expiration date
    /// </summary>
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

        // T133: Indefinite retention (RetentionDays = 0)
        if (policy.RetentionDays == 0)
        {
            _logger.LogInformation("Applying indefinite retention policy {PolicyName} to file {FileId}",
                policy.PolicyName, fileMetadata.FileId);
            return null; // No expiration
        }

        // Calculate expiration date
        var expiresAt = fileMetadata.UploadedAt.AddDays(policy.RetentionDays);

        _logger.LogInformation(
            "Applied retention policy {PolicyName} ({RetentionDays} days) to file {FileId}. Expires at {ExpiresAt}",
            policy.PolicyName, policy.RetentionDays, fileMetadata.FileId, expiresAt);

        return expiresAt;
    }

    /// <summary>
    /// Gets the active retention policy for a service and path
    /// </summary>
    public async Task<RetentionPolicy?> GetActiveRetentionPolicyAsync(
        string serviceId,
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        // Query for active policies matching the service
        var query = _context.RetentionPolicies
            .Where(p => p.IsActive && (p.ServiceId == serviceId || p.ServiceId == null))
            .OrderByDescending(p => p.ServiceId != null) // Prefer service-specific policies
            .ThenByDescending(p => p.ApplyToPathPrefix!.Length); // Prefer more specific paths

        // Filter by path prefix if specified
        var policies = await query.ToListAsync(cancellationToken);

        foreach (var policy in policies)
        {
            // If no path prefix specified, policy applies to all paths
            if (string.IsNullOrEmpty(policy.ApplyToPathPrefix))
            {
                return policy;
            }

            // Check if storage path matches the policy's path prefix
            if (storagePath.StartsWith(policy.ApplyToPathPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return policy;
            }
        }

        _logger.LogDebug("No active retention policy found for service {ServiceId} and path {StoragePath}",
            serviceId, storagePath);

        return null;
    }

    /// <summary>
    /// Determines the appropriate storage class based on file age and transition rules
    /// </summary>
    public string GetStorageClassForAge(int ageInDays, List<StorageClassTransition>? transitions)
    {
        if (transitions == null || transitions.Count == 0)
        {
            return "STANDARD";
        }

        // Sort transitions by days ascending (should already be sorted, but ensure it)
        var sortedTransitions = transitions.OrderBy(t => t.Days).ToList();

        // Find the appropriate storage class based on age
        string storageClass = "STANDARD";
        foreach (var transition in sortedTransitions)
        {
            if (ageInDays >= transition.Days)
            {
                storageClass = transition.StorageClass;
            }
            else
            {
                break; // Stop when we reach a transition that hasn't occurred yet
            }
        }

        return storageClass;
    }

    /// <summary>
    /// Processes expired files based on retention policies
    /// NOTE: In production, this would mark files for deletion in GCS via lifecycle rules
    /// For now, we'll mark them in the database and log for manual cleanup
    /// </summary>
    public async Task<int> ProcessExpiredFilesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Find files that have expired
        var expiredFiles = await _context.FileMetadata
            .Where(f => f.ExpiresAt != null && f.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        if (expiredFiles.Count == 0)
        {
            _logger.LogDebug("No expired files found for processing");
            return 0;
        }

        _logger.LogInformation("Found {Count} expired files for processing", expiredFiles.Count);

        // In a real implementation, we would:
        // 1. Call GCS API to delete the files
        // 2. Remove records from database
        // For now, we'll just log them

        foreach (var file in expiredFiles)
        {
            _logger.LogInformation(
                "File {FileId} at path {StoragePath} expired on {ExpiresAt}. Deleting from GCS and database.",
                file.FileId, file.StoragePath, file.ExpiresAt);

            try
            {
                await _storageService.DeleteFileAsync(file.StoragePath, cancellationToken);
                _context.FileMetadata.Remove(file);
                // Also remove the Upload entry if possible, but FileMetadata is the main record here
                // Note: This logic assumes 1:1 Upload:FileMetadata or that we are cleaning up metadata.
                // Ideally we should check if other metadata points to same upload, but Upload is usually parent.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete expired file {FileId} at {StoragePath}", file.FileId, file.StoragePath);
                // Continue to next file
                continue;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return expiredFiles.Count;
    }

    /// <summary>
    /// Updates storage classes for files based on age and transition rules
    /// NOTE: In production, this would update GCS object storage classes
    /// </summary>
    public async Task<int> UpdateStorageClassesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        int updatedCount = 0;

        // Get all files with retention policies that have storage class transitions
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

            // Calculate file age
            var ageInDays = (int)(now - file.UploadedAt).TotalDays;

            // Determine appropriate storage class
            var targetStorageClass = GetStorageClassForAge(ageInDays, file.RetentionPolicy.StorageClassTransitions);

            // Check if storage class needs to be updated
            if (file.StorageClass != targetStorageClass)
            {
                _logger.LogInformation(
                    "File {FileId} ({AgeInDays} days old) should transition from {CurrentClass} to {TargetClass}",
                    file.FileId, ageInDays, file.StorageClass, targetStorageClass);

                try
                {
                    // Call GCS API to update the object's storage class
                    // Note: We'll assume the storage service handles the actual transition details
                    await _storageService.UpdateStorageClassAsync(file.StoragePath, targetStorageClass, cancellationToken);

                    // Update storage class in database
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
