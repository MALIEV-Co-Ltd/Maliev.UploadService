using Maliev.Aspire.ServiceDefaults.IAM;
using Maliev.UploadService.Api.Metrics;
using Maliev.UploadService.Api.Services.Auth;
using Maliev.UploadService.Domain.Entities;
using Maliev.UploadService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace Maliev.UploadService.Api.Services;

/// <summary>
/// Service for managing authorization policies and access control.
/// </summary>
public class AuthorizationPolicyService : IAuthorizationPolicyService
{
    private readonly UploadDbContext _context;
    private readonly IDistributedCache _cache;
    private readonly ILogger<AuthorizationPolicyService> _logger;
    private readonly IIamServiceClient _iamClient;
    private readonly UploadMetrics _metrics;
    private readonly TimeSpan _cacheDuration;

    /// <summary>
    /// Initializes a new instance of the AuthorizationPolicyService class.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="cache">The distributed cache.</param>
    /// <param name="logger">The logger for this service.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="iamClient">The IAM service client.</param>
    /// <param name="metrics">The upload metrics.</param>
    public AuthorizationPolicyService(
        UploadDbContext context,
        IDistributedCache cache,
        ILogger<AuthorizationPolicyService> logger,
        IConfiguration configuration,
        IIamServiceClient iamClient,
        UploadMetrics metrics)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
        _iamClient = iamClient;
        _metrics = metrics;
        _cacheDuration = TimeSpan.FromMinutes(
            configuration.GetValue<int>("Authorization:PolicyCacheDurationMinutes", 5));
    }

    /// <summary>
    /// Retrieves the authorization policy for a given service ID.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The authorization policy if found, otherwise null.</returns>
    public async Task<ServiceAuthorizationPolicy?> GetPolicyAsync(string serviceId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"authz_policy:{serviceId}";

        // Try cache first
        var cachedPolicy = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cachedPolicy))
        {
            return JsonSerializer.Deserialize<ServiceAuthorizationPolicy>(cachedPolicy);
        }

        // Query database
        var policy = await _context.ServiceAuthorizationPolicies
            .Where(p => p.ServiceId == serviceId && p.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        if (policy != null)
        {
            // Cache the policy
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _cacheDuration
            };
            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(policy),
                options,
                cancellationToken);
        }

        return policy;
    }

    /// <summary>
    /// Checks if a service can upload to a specific path.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="path">The storage path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the service can upload to the path, otherwise false.</returns>
    public async Task<bool> CanUploadToPathAsync(string serviceId, string path, CancellationToken cancellationToken = default)
    {
        // 1. IAM Check (Overrides Legacy)
        var resourcePath = $"folders/{path.TrimStart('/')}";
        var iamPrincipalId = ToIamPrincipalId(serviceId);
        var isAuthorizedViaIAM = await _iamClient.CheckPermissionAsync(
            iamPrincipalId,
            UploadPermissions.FilesUpload,
            resourcePath,
            cancellationToken);

        if (isAuthorizedViaIAM)
        {
            _metrics.RecordAuthSuccess(UploadPermissions.FilesUpload, resourcePath);
            return true;
        }

        // 2. Legacy Fallback
        var policy = await GetPolicyAsync(serviceId, cancellationToken);
        if (policy == null)
        {
            _logger.LogWarning("No authorization policy found for service {ServiceId}", serviceId);
            _metrics.RecordAuthFailure(UploadPermissions.FilesUpload, resourcePath, "NoPolicy");
            return false;
        }

        // Check if path matches any allowed prefix
        var authorized = policy.AllowedPathPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        if (!authorized)
        {
            _metrics.RecordAuthFailure(UploadPermissions.FilesUpload, resourcePath, "LegacyDenial");
        }
        else
        {
            _logger.LogInformation("Authorized via Legacy Policy for {ServiceId} on {Path}", serviceId, path);
        }

        return authorized;
    }

    /// <inheritdoc />
    public async Task<bool> AuthorizePathLiveAsync(
        string principalId,
        string? legacyPolicyServiceId,
        string permissionId,
        string sanitizedPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(principalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sanitizedPath);

        var resourcePath = $"folders/{sanitizedPath.TrimStart('/')}";
        if (await _iamClient.CheckPermissionLiveAsync(
                principalId,
                permissionId,
                resourcePath,
                cancellationToken))
        {
            _metrics.RecordAuthSuccess(permissionId, resourcePath);
            return true;
        }

        if (string.IsNullOrWhiteSpace(legacyPolicyServiceId))
        {
            _metrics.RecordAuthFailure(permissionId, resourcePath, "LiveIamDenial");
            return false;
        }

        var policy = await GetPolicyAsync(legacyPolicyServiceId, cancellationToken);
        var allowed = policy is not null && policy.AllowedPathPrefixes.Any(
            prefix => IsPathWithinPrefix(sanitizedPath, prefix));
        if (!allowed)
        {
            _metrics.RecordAuthFailure(permissionId, resourcePath, "LegacyDenial");
        }

        return allowed;
    }

    private static bool IsPathWithinPrefix(string path, string prefix)
    {
        var normalizedPrefix = prefix.Trim('/');
        return path.Equals(normalizedPrefix, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith($"{normalizedPrefix}/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a service can access (read/delete) a file at a specific path.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="path">The storage path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the service can access the path, otherwise false.</returns>
    public async Task<bool> CanAccessPathAsync(string serviceId, string path, CancellationToken cancellationToken = default)
    {
        // 1. IAM Check (Overrides Legacy)
        var resourcePath = $"folders/{path.TrimStart('/')}";
        var iamPrincipalId = ToIamPrincipalId(serviceId);
        var isAuthorizedViaIAM = await _iamClient.CheckPermissionAsync(
            iamPrincipalId,
            UploadPermissions.FilesRead, // Accessing a path implies reading it
            resourcePath,
            cancellationToken);

        if (isAuthorizedViaIAM)
        {
            _metrics.RecordAuthSuccess(UploadPermissions.FilesRead, resourcePath);
            return true;
        }

        // 2. Legacy Fallback
        return await CanUploadToPathAsync(serviceId, path, cancellationToken);
    }

    /// <summary>
    /// Validates if a content type is allowed for a service.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="contentType">The content type.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the content type is allowed, otherwise false.</returns>
    public async Task<bool> IsContentTypeAllowedAsync(string serviceId, string contentType, CancellationToken cancellationToken = default)
    {
        var policy = await GetPolicyAsync(serviceId, cancellationToken);
        if (policy == null)
        {
            _logger.LogWarning("No authorization policy found for service {ServiceId}", serviceId);
            return false;
        }

        return policy.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates if a file size is within the service's limit.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="fileSize">The file size in bytes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the file size is allowed, otherwise false.</returns>
    public async Task<bool> IsFileSizeAllowedAsync(string serviceId, long fileSize, CancellationToken cancellationToken = default)
    {
        var policy = await GetPolicyAsync(serviceId, cancellationToken);
        if (policy == null)
        {
            _logger.LogWarning("No authorization policy found for service {ServiceId}", serviceId);
            return false;
        }

        return fileSize <= policy.MaxFileSizeBytes;
    }

    /// <summary>
    /// Checks if a service can overwrite existing files.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if overwrite is allowed, otherwise false.</returns>
    public async Task<bool> CanOverwriteAsync(string serviceId, CancellationToken cancellationToken = default)
    {
        var policy = await GetPolicyAsync(serviceId, cancellationToken);
        return policy?.AllowOverwrite ?? false;
    }

    /// <summary>
    /// Checks if a service has remaining storage quota for a given file size.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="fileSize">The file size in bytes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the service has sufficient quota, otherwise false.</returns>
    public async Task<bool> HasStorageQuotaAsync(string serviceId, long fileSize, CancellationToken cancellationToken = default)
    {
        var policy = await GetPolicyAsync(serviceId, cancellationToken);
        if (policy == null)
        {
            _logger.LogWarning("No authorization policy found for service {ServiceId}", serviceId);
            return false;
        }

        // Calculate current storage usage
        var currentUsage = await _context.FileMetadata
            .Where(f => f.ServiceId == serviceId)
            .SumAsync(f => f.FileSize, cancellationToken);

        var wouldExceed = (currentUsage + fileSize) > policy.StorageQuotaBytes;

        if (wouldExceed)
        {
            _logger.LogWarning(
                "Service {ServiceId} would exceed storage quota. Current: {Current}, Quota: {Quota}, Requested: {Requested}",
                serviceId, currentUsage, policy.StorageQuotaBytes, fileSize);
        }

        return !wouldExceed;
    }

    private static string ToIamPrincipalId(string serviceId)
    {
        var trimmedServiceId = serviceId.Trim();
        if (Guid.TryParse(trimmedServiceId, out _) ||
            trimmedServiceId.Contains('@', StringComparison.Ordinal) ||
            trimmedServiceId.StartsWith("system:service:", StringComparison.OrdinalIgnoreCase))
        {
            return trimmedServiceId;
        }

        if (trimmedServiceId.EndsWith("Service", StringComparison.Ordinal))
        {
            var serviceName = trimmedServiceId[..^"Service".Length]
                .Trim('-', '_', '.', ' ')
                .ToLowerInvariant();
            return $"system:service:{serviceName}";
        }

        if (trimmedServiceId.IndexOfAny(['-', '_', '.', ':']) >= 0)
        {
            return trimmedServiceId;
        }

        return $"system:service:{trimmedServiceId.ToLowerInvariant()}";
    }
}
