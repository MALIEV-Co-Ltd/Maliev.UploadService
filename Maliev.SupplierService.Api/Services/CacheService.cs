using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace Maliev.SupplierService.Api.Services;

public class CacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<CacheService> _logger;

    // TTL strategies per research.md
    private static readonly TimeSpan DefaultSupplierTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DefaultListTtl = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan DefaultEligibilityTtl = TimeSpan.FromMinutes(1);

    public CacheService(
        IDistributedCache cache,
        ILogger<CacheService> logger,
        IConnectionMultiplexer? redis = null)
    {
        _cache = cache;
        _logger = logger;
        _redis = redis;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var cached = await _cache.GetStringAsync(key, cancellationToken);
            if (cached is null)
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(cached);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get cache key {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ?? GetDefaultTtl(key)
            };

            await _cache.SetStringAsync(key, json, options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set cache key {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _cache.RemoveAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove cache key {Key}", key);
        }
    }

    public async Task InvalidateByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        if (_redis is null)
        {
            _logger.LogDebug("Redis not available for tag invalidation: {Tag}", tag);
            return;
        }

        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var db = _redis.GetDatabase();

            // Find keys matching the tag pattern and delete them
            await foreach (var key in server.KeysAsync(pattern: $"{tag}:*"))
            {
                await db.KeyDeleteAsync(key);
            }

            _logger.LogDebug("Invalidated cache keys with tag: {Tag}", tag);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate cache by tag {Tag}", tag);
        }
    }

    private static TimeSpan GetDefaultTtl(string key) => key switch
    {
        _ when key.StartsWith("supplier:") => DefaultSupplierTtl,
        _ when key.StartsWith("suppliers:") => DefaultListTtl,
        _ when key.Contains("eligibility") => DefaultEligibilityTtl,
        _ => DefaultSupplierTtl
    };
}
