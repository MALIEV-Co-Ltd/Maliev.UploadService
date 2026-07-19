using Maliev.UploadService.Domain.Entities;

namespace Maliev.UploadService.Application.Interfaces;

/// <summary>
/// Manages file lifecycle: retention policy application, expiry processing, and storage class transitions.
/// </summary>
public interface ILifecycleManagementService
{
    /// <summary>
    /// Applies a retention policy to a file and calculates its expiration date.
    /// </summary>
    /// <param name="fileMetadata">The file to apply the policy to.</param>
    /// <param name="policyId">The retention policy identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The calculated expiration date, or <c>null</c> for indefinite retention.</returns>
    Task<DateTime?> ApplyRetentionPolicyAsync(FileMetadata fileMetadata, string policyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the most specific active retention policy for a given service and file path.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="storagePath">The storage path of the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching <see cref="RetentionPolicy"/>, or <c>null</c> if none applies.</returns>
    Task<RetentionPolicy?> GetActiveRetentionPolicyAsync(string serviceId, string storagePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines the appropriate GCS storage class for a file based on its age and transition rules.
    /// </summary>
    /// <param name="ageInDays">The file age in days.</param>
    /// <param name="transitions">The ordered list of storage class transition rules.</param>
    /// <returns>The storage class string (e.g., STANDARD, NEARLINE, COLDLINE, ARCHIVE).</returns>
    string GetStorageClassForAge(int ageInDays, List<StorageClassTransition>? transitions);

    /// <summary>
    /// Scans for expired files and deletes them from both GCS and the database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of expired files processed.</returns>
    Task<int> ProcessExpiredFilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks all files with retention policies and transitions them to the correct storage class.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of files whose storage class was updated.</returns>
    Task<int> UpdateStorageClassesAsync(CancellationToken cancellationToken = default);
}
