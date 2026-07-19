using Maliev.Aspire.ServiceDefaults.IAM;
using Maliev.UploadService.Application.Interfaces;
using Maliev.UploadService.Domain.Entities;
using Maliev.UploadService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Maliev.UploadService.Infrastructure.Services;

/// <summary>
/// Provides authorization checks using the legacy policy store as a fallback
/// when the central IAM service denies or is unavailable.
/// </summary>
public class AuthorizationPolicyService : IAuthorizationPolicyService
{
    private readonly UploadDbContext _context;
    private readonly IDistributedCache _cache;
    private readonly ILogger<AuthorizationPolicyService> _logger;
    private readonly IIamServiceClient _iamClient;
    private readonly TimeSpan _cacheDuration;

    /// <summary>
    /// Initializes a new instance of <see cref="AuthorizationPolicyService"/>.
    /// </summary>
    public AuthorizationPolicyService(
        UploadDbContext context,
        IDistributedCache cache,
        ILogger<AuthorizationPolicyService> logger,
        IConfiguration configuration,
        IIamServiceClient iamClient)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
        _iamClient = iamClient;
        _cacheDuration = TimeSpan.FromMinutes(
            configuration.GetValue<int>("Authorization:PolicyCacheDurationMinutes", 5));
    }

    /// <inheritdoc/>
    public async Task<ServiceAuthorizationPolicy?> GetPolicyAsync(string serviceId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"authz_policy:{serviceId}";

        var cachedPolicy = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cachedPolicy))
        {
            return JsonSerializer.Deserialize<ServiceAuthorizationPolicy>(cachedPolicy);
        }

        var policy = await _context.ServiceAuthorizationPolicies
            .Where(p => p.ServiceId == serviceId && p.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        if (policy != null)
        {
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

    /// <inheritdoc/>
    public async Task<bool> CanUploadToPathAsync(string serviceId, string path, CancellationToken cancellationToken = default)
    {
        var resourcePath = $"folders/{path.TrimStart('/')}";
        var isAuthorizedViaIAM = await _iamClient.CheckPermissionAsync(
            serviceId,
            "upload.files.upload",
            resourcePath,
            cancellationToken);

        if (isAuthorizedViaIAM)
        {
            return true;
        }

        var policy = await GetPolicyAsync(serviceId, cancellationToken);
        if (policy == null)
        {
            _logger.LogWarning("No authorization policy found for service {ServiceId}", serviceId);
            return false;
        }

        return policy.AllowedPathPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public async Task<bool> CanAccessPathAsync(string serviceId, string path, CancellationToken cancellationToken = default)
    {
        var resourcePath = $"folders/{path.TrimStart('/')}";
        var isAuthorizedViaIAM = await _iamClient.CheckPermissionAsync(
            serviceId,
            "upload.files.read",
            resourcePath,
            cancellationToken);

        if (isAuthorizedViaIAM)
        {
            return true;
        }

        return await CanUploadToPathAsync(serviceId, path, cancellationToken);
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public async Task<bool> CanOverwriteAsync(string serviceId, CancellationToken cancellationToken = default)
    {
        var policy = await GetPolicyAsync(serviceId, cancellationToken);
        return policy?.AllowOverwrite ?? false;
    }

    /// <inheritdoc/>
    public async Task<bool> HasStorageQuotaAsync(string serviceId, long fileSize, CancellationToken cancellationToken = default)
    {
        var policy = await GetPolicyAsync(serviceId, cancellationToken);
        if (policy == null)
        {
            _logger.LogWarning("No authorization policy found for service {ServiceId}", serviceId);
            return false;
        }

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
}
