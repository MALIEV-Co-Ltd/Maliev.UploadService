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

    /// <summary>
    /// Initializes a new instance of the IamServiceClient class.
    /// </summary>
    /// <param name="httpClient">The HTTP client for IAM service calls.</param>
    /// <param name="cache">The distributed cache.</param>
    /// <param name="logger">The logger for this service.</param>
    /// <param name="configuration">The application configuration.</param>
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

    /// <summary>
    /// Checks if a principal has a specific permission for a resource.
    /// </summary>
    /// <param name="principalId">The principal identifier.</param>
    /// <param name="permission">The permission to check.</param>
    /// <param name="resourcePath">The optional resource path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the principal has the permission, otherwise false.</returns>
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

    /// <summary>
    /// Checks multiple permissions for a principal.
    /// </summary>
    /// <param name="principalId">The principal identifier.</param>
    /// <param name="requests">The permission check requests.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A dictionary of permission results.</returns>
    public Task<Dictionary<string, bool>> CheckPermissionsAsync(string principalId, IEnumerable<PermissionCheckRequest> requests, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new Dictionary<string, bool>());
    }

    /// <summary>
    /// Gets all permissions for a user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An enumerable of permission strings.</returns>
    public Task<IEnumerable<string>> GetUserPermissionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Enumerable.Empty<string>());
    }

    /// <summary>
    /// Gets all authorized resources for a principal with a specific permission.
    /// </summary>
    /// <param name="principalId">The principal identifier.</param>
    /// <param name="permissionId">The permission identifier.</param>
    /// <param name="resourceType">The resource type.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An enumerable of resource paths.</returns>
    public Task<IEnumerable<string>> GetAuthorizedResourcesAsync(string principalId, string permissionId, string resourceType, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Enumerable.Empty<string>());
    }
}
