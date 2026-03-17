using Maliev.UploadService.Domain.Entities;

namespace Maliev.UploadService.Api.Services;

/// <summary>
/// Interface for bulk delete operations (FR-032, FR-033)
/// </summary>
public interface IBulkDeleteService
{
    /// <summary>
    /// Initiates a new bulk delete job.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="pathPrefix">Optional path prefix to filter files.</param>
    /// <param name="uploadIds">Optional list of specific upload IDs to delete.</param>
    /// <param name="deleteFilesOlderThan">Optional date threshold for deletion.</param>
    /// <param name="reason">The reason for the bulk delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The job ID.</returns>
    Task<string> InitiateBulkDeleteAsync(
        string serviceId,
        string? pathPrefix,
        List<string>? uploadIds,
        DateTime? deleteFilesOlderThan,
        string? reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the status of a bulk delete job.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The bulk delete job if found, otherwise null.</returns>
    Task<BulkDeleteJob?> GetBulkDeleteJobStatusAsync(
        string jobId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a bulk delete job by deleting files matching the criteria.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ProcessBulkDeleteJobAsync(
        string jobId,
        CancellationToken cancellationToken = default);
}
