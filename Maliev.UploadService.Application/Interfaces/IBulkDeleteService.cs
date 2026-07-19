using Maliev.UploadService.Domain.Entities;

namespace Maliev.UploadService.Application.Interfaces;

/// <summary>
/// Orchestrates bulk file deletion operations (FR-032, FR-033).
/// </summary>
public interface IBulkDeleteService
{
    /// <summary>
    /// Creates a new bulk delete job and queues it for background processing.
    /// </summary>
    /// <param name="serviceId">The service whose files should be deleted.</param>
    /// <param name="pathPrefix">Optional path prefix filter.</param>
    /// <param name="uploadIds">Optional list of specific upload IDs to delete.</param>
    /// <param name="deleteFilesOlderThan">Optional cutoff date — only older files are deleted.</param>
    /// <param name="reason">Optional reason for the bulk deletion.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The job ID of the created bulk delete job.</returns>
    Task<string> InitiateBulkDeleteAsync(
        string serviceId,
        string? pathPrefix,
        List<string>? uploadIds,
        DateTime? deleteFilesOlderThan,
        string? reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the current status of a bulk delete job.
    /// </summary>
    /// <param name="jobId">The bulk delete job identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The <see cref="BulkDeleteJob"/>, or <c>null</c> if not found.</returns>
    Task<BulkDeleteJob?> GetBulkDeleteJobStatusAsync(
        string jobId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the actual file deletion for a queued bulk delete job.
    /// </summary>
    /// <param name="jobId">The bulk delete job identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ProcessBulkDeleteJobAsync(
        string jobId,
        CancellationToken cancellationToken = default);
}
