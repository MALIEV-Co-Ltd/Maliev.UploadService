using Maliev.UploadService.Data;
using Maliev.UploadService.Data.Entities;
using Maliev.UploadService.Api.Services.Auth;
using Maliev.UploadService.Api.Metrics;
using Maliev.Aspire.ServiceDefaults.IAM;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace Maliev.UploadService.Api.Services;

public class AuthorizationPolicyService : IAuthorizationPolicyService
{
    private readonly UploadDbContext _context;
    private readonly IDistributedCache _cache;
    private readonly ILogger<AuthorizationPolicyService> _logger;
    private readonly IIamServiceClient _iamClient;
    private readonly UploadMetrics _metrics;
    private readonly TimeSpan _cacheDuration;

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

    public async Task<bool> CanUploadToPathAsync(string serviceId, string path, CancellationToken cancellationToken = default)
    {
        // 1. IAM Check (Overrides Legacy)
        var resourcePath = $"folders/{path.TrimStart('/')}";
        var isAuthorizedViaIAM = await _iamClient.CheckPermissionAsync(
            serviceId,
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

    public async Task<bool> CanAccessPathAsync(string serviceId, string path, CancellationToken cancellationToken = default)
    {
        // 1. IAM Check (Overrides Legacy)
        var resourcePath = $"folders/{path.TrimStart('/')}";
        var isAuthorizedViaIAM = await _iamClient.CheckPermissionAsync(
            serviceId,
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

    public async Task<bool> CanOverwriteAsync(string serviceId, CancellationToken cancellationToken = default)
    {
        var policy = await GetPolicyAsync(serviceId, cancellationToken);
        return policy?.AllowOverwrite ?? false;
    }

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
}

