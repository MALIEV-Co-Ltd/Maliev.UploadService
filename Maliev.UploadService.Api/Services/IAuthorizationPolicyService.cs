using Maliev.UploadService.Domain.Entities;

namespace Maliev.UploadService.Api.Services;

public interface IAuthorizationPolicyService
{
    /// <summary>
    /// Retrieves the authorization policy for a given service ID
    /// </summary>
    Task<ServiceAuthorizationPolicy?> GetPolicyAsync(string serviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a service can upload to a specific path
    /// </summary>
    Task<bool> CanUploadToPathAsync(string serviceId, string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a service can access (read/delete) a file at a specific path
    /// </summary>
    Task<bool> CanAccessPathAsync(string serviceId, string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates if a content type is allowed for a service
    /// </summary>
    Task<bool> IsContentTypeAllowedAsync(string serviceId, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates if a file size is within the service's limit
    /// </summary>
    Task<bool> IsFileSizeAllowedAsync(string serviceId, long fileSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a service can overwrite existing files
    /// </summary>
    Task<bool> CanOverwriteAsync(string serviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a service has remaining storage quota
    /// </summary>
    Task<bool> HasStorageQuotaAsync(string serviceId, long fileSize, CancellationToken cancellationToken = default);
}
