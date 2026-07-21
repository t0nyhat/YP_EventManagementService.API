namespace EventManagementService.Events.Application.Caching;

/// <summary>
/// Single source of truth for cache key formats used by the Events service.
/// All cache keys must be produced here so every consumer uses the same format.
/// </summary>
public static class EventCacheKeys
{
    /// <summary>
    /// Cache key for the top-10 events projection.
    /// </summary>
    public const string Top10 = "events:top10";

    /// <summary>
    /// Builds the cache key for a single event.
    /// The identifier is rendered in GUID "D" format
    /// (lowercase, hyphen-separated) to keep one format everywhere.
    /// </summary>
    /// <param name="id">The unique identifier of the event.</param>
    /// <returns>The cache key in the form <c>event:{id}</c>.</returns>
    public static string ForEvent(Guid id) => $"event:{id:D}";
}
