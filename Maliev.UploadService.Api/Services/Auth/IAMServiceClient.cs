using Maliev.Aspire.ServiceDefaults.IAM;
using Microsoft.Extensions.Caching.Distributed;

namespace Maliev.UploadService.Api.Services.Auth;

/// <summary>
/// Implementation of IIamServiceClient that calls the central IAM Service.
/// Implements 5-minute caching logic as per Constitution Principle V.
/// </summary>
public class IamServiceClient : IIamServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly IDistributedCache _cache;
    private readonly ILogger<IamServiceClient> _logger;
    private readonly TimeSpan _cacheTtl;

    public IamServiceClient(
        HttpClient httpClient,
        IDistributedCache cache,
        ILogger<IamServiceClient> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _cacheTtl = TimeSpan.FromMinutes(configuration.GetValue<int>("IAM:CacheTtlMinutes", 5));
    }

    public async Task<bool> CheckPermissionAsync(string principalId, string permission, string? resourcePath = null, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"iam_perm:{principalId}:{permission}:{resourcePath ?? "root"}";

        try
        {
            // Try cache first
            var cachedResult = await _cache.GetStringAsync(cacheKey, cancellationToken);
            if (cachedResult != null)
            {
                return bool.Parse(cachedResult);
            }

            // Call IAM Service
            // Endpoint: GET /v1/permissions/check?principal={principalId}&permission={permission}&resource={resourcePath}
            // BaseAddress is handled by Service Discovery in Program.cs
            var query = $"?principal={Uri.EscapeDataString(principalId)}&permission={Uri.EscapeDataString(permission)}";
            if (!string.IsNullOrEmpty(resourcePath))
            {
                query += $"&resource={Uri.EscapeDataString(resourcePath)}";
            }

            var response = await _httpClient.GetAsync($"/v1/permissions/check{query}", cancellationToken);
            var isAuthorized = response.IsSuccessStatusCode;

            // Cache the result
            await _cache.SetStringAsync(cacheKey, isAuthorized.ToString(), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _cacheTtl
            }, cancellationToken);

            return isAuthorized;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check permission {Permission} for resource {ResourcePath} via IAM Service",
                permission, resourcePath);
            return false;
        }
    }

    public Task<Dictionary<string, bool>> CheckPermissionsAsync(string principalId, IEnumerable<PermissionCheckRequest> requests, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new Dictionary<string, bool>());
    }

    public Task<IEnumerable<string>> GetUserPermissionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Enumerable.Empty<string>());
    }
}
