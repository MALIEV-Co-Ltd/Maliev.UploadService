using Maliev.UploadService.Api.Models.Entities;

namespace Maliev.UploadService.Api.Services;

/// <summary>
/// T168: Interface for bulk delete operations (FR-032, FR-033)
/// </summary>
public interface IBulkDeleteService
{
    Task<string> InitiateBulkDeleteAsync(
        string serviceId,
        string? pathPrefix,
        List<string>? uploadIds,
        DateTime? deleteFilesOlderThan,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<BulkDeleteJob?> GetBulkDeleteJobStatusAsync(
        string jobId,
        CancellationToken cancellationToken = default);

    Task ProcessBulkDeleteJobAsync(
        string jobId,
        CancellationToken cancellationToken = default);
}
