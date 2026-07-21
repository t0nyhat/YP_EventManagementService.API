using System.Text.Json;
using EventManagementService.Events.Application.Abstractions.Caching;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace EventManagementService.Events.Infrastructure.Caching;

/// <summary>
/// Redis-backed implementation of <see cref="ICacheService"/>.
/// Best-effort by contract: infrastructure failures never propagate to callers —
/// a failed read is a cache miss, failed writes/removals are logged no-ops.
/// Invalid argument usage (empty key, null value) is a programming error and throws.
/// <see cref="OperationCanceledException"/> is never swallowed and flows to the caller.
/// </summary>
public sealed class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _connection;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer connection, ILogger<RedisCacheService> logger)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        RedisValue payload;
        try
        {
            // Async API StackExchange.Redis не принимает CancellationToken,
            // поэтому WaitAsync позволяет вызывающему прервать ожидание зависшего вызова.
            payload = await _connection.GetDatabase()
                .StringGetAsync(key)
                .WaitAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                exception,
                "Cache get failed for key {CacheKey}. Treating as a cache miss.",
                key);
            return null;
        }

        if (payload.IsNullOrEmpty)
        {
            _logger.LogDebug("Cache miss for key {CacheKey}.", key);
            return null;
        }

        try
        {
            var value = JsonSerializer.Deserialize<T>(payload.ToString(), CacheJson.Options);
            if (value is null)
            {
                _logger.LogDebug("Cache entry for key {CacheKey} deserialized to null. Treating as a cache miss.", key);
                return null;
            }

            _logger.LogDebug("Cache hit for key {CacheKey}.", key);
            return value;
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(
                exception,
                "Cache entry for key {CacheKey} contains invalid JSON. Removing the corrupted entry and treating as a cache miss.",
                key);
            await TryRemoveCorruptedEntryAsync(key);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var payload = JsonSerializer.Serialize(value, CacheJson.Options);
            await _connection.GetDatabase()
                .StringSetAsync(key, payload, timeToLive, When.Always, CommandFlags.None)
                .WaitAsync(cancellationToken);
            _logger.LogDebug("Cached entry for key {CacheKey} with TTL {TimeToLive}.", key, timeToLive);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                exception,
                "Cache set failed for key {CacheKey}. Skipping the cache write.",
                key);
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await _connection.GetDatabase()
                .KeyDeleteAsync(key)
                .WaitAsync(cancellationToken);
            _logger.LogDebug("Removed cache entry for key {CacheKey}.", key);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                exception,
                "Cache remove failed for key {CacheKey}. Skipping the cache removal.",
                key);
        }
    }

    /// <summary>
    /// Best-effort removal of a corrupted cache entry so it cannot poison later reads.
    /// Deliberately not cancellable: the read already produced its result (a miss),
    /// and failures here are logged and swallowed.
    /// </summary>
    private async Task TryRemoveCorruptedEntryAsync(string key)
    {
        try
        {
            await _connection.GetDatabase().KeyDeleteAsync(key);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                exception,
                "Failed to remove corrupted cache entry for key {CacheKey}.",
                key);
        }
    }
}
