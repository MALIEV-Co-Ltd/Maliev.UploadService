using Maliev.UploadService.Data.Entities;

namespace Maliev.UploadService.Api.Services;

/// <summary>
/// Service interface for managing file lifecycle and retention policies
/// </summary>
public interface ILifecycleManagementService
{
    /// <summary>
    /// Applies a retention policy to a file and calculates expiration date
    /// </summary>
    /// <param name="fileMetadata">File metadata to apply policy to</param>
    /// <param name="policyId">Retention policy ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Calculated expiration date, or null if indefinite retention</returns>
    Task<DateTime?> ApplyRetentionPolicyAsync(FileMetadata fileMetadata, string policyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active retention policy for a service and path
    /// </summary>
    /// <param name="serviceId">Service ID</param>
    /// <param name="storagePath">File storage path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Matching retention policy, or null if none found</returns>
    Task<RetentionPolicy?> GetActiveRetentionPolicyAsync(string serviceId, string storagePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines the appropriate storage class based on file age and transition rules
    /// </summary>
    /// <param name="ageInDays">File age in days</param>
    /// <param name="transitions">Storage class transition rules</param>
    /// <returns>Appropriate storage class (STANDARD, NEARLINE, COLDLINE, ARCHIVE)</returns>
    string GetStorageClassForAge(int ageInDays, List<StorageClassTransition>? transitions);

    /// <summary>
    /// Processes expired files based on retention policies
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of files processed</returns>
    Task<int> ProcessExpiredFilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates storage classes for files based on age and transition rules
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of files updated</returns>
    Task<int> UpdateStorageClassesAsync(CancellationToken cancellationToken = default);
}
