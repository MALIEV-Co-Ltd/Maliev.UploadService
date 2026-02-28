using Maliev.UploadService.Domain.Entities;

namespace Maliev.UploadService.Application.Interfaces;

/// <summary>
/// Provides authorization checks for upload and file access operations using the legacy policy store
/// as a fallback when the central IAM service denies or is unavailable.
/// </summary>
public interface IAuthorizationPolicyService
{
    /// <summary>
    /// Retrieves the authorization policy for a given service.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The service's authorization policy, or <c>null</c> if no policy exists.</returns>
    Task<ServiceAuthorizationPolicy?> GetPolicyAsync(string serviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a service is permitted to upload to the specified path.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="path">The target storage path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if upload is permitted; otherwise <c>false</c>.</returns>
    Task<bool> CanUploadToPathAsync(string serviceId, string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a service may read or delete a file at the specified path.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="path">The storage path of the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if access is permitted; otherwise <c>false</c>.</returns>
    Task<bool> CanAccessPathAsync(string serviceId, string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a content type is permitted for a service.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="contentType">The MIME content type to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the content type is allowed; otherwise <c>false</c>.</returns>
    Task<bool> IsContentTypeAllowedAsync(string serviceId, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a file size is within the service's maximum upload limit.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="fileSize">The file size in bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the size is allowed; otherwise <c>false</c>.</returns>
    Task<bool> IsFileSizeAllowedAsync(string serviceId, long fileSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a service is permitted to overwrite existing files.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if overwrite is permitted; otherwise <c>false</c>.</returns>
    Task<bool> CanOverwriteAsync(string serviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a service has sufficient remaining storage quota for the given file size.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="fileSize">The file size in bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the quota is sufficient; otherwise <c>false</c>.</returns>
    Task<bool> HasStorageQuotaAsync(string serviceId, long fileSize, CancellationToken cancellationToken = default);
}
