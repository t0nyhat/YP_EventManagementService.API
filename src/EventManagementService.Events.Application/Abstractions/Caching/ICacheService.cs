namespace EventManagementService.Events.Application.Abstractions.Caching;

/// <summary>
/// Best-effort cache port for the Events service.
/// Implementations must not propagate infrastructure failures to callers:
/// a failed read is reported as a cache miss and failed writes/removals
/// complete as logged no-ops, so business flows keep working without the cache.
/// <see cref="OperationCanceledException"/> is not masked and flows to the caller.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Retrieves a cached value by key.
    /// </summary>
    /// <typeparam name="T">The type the cached payload is deserialized into.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// The cached value, or <c>null</c> when the key is absent
    /// or the cache is unavailable (treated as a miss).
    /// </returns>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>
    /// Stores a value in the cache with the specified time-to-live.
    /// Infrastructure failures are logged and swallowed (no-op).
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="timeToLive">How long the entry stays in the cache.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task SetAsync<T>(
        string key,
        T value,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>
    /// Removes a value from the cache.
    /// Infrastructure failures are logged and swallowed (no-op).
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
