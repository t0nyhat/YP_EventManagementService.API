namespace EventManagementService.Events.Application.Configuration;

/// <summary>
/// Cache TTL policy for the Events service, bound from the "Cache" configuration section.
/// Binding and validation are performed in Infrastructure DI.
/// </summary>
public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>
    /// Time-to-live for a single event entry. Defaults to 10 minutes:
    /// the entry is proactively invalidated by write paths (update/delete,
    /// booking confirmations), so the TTL only bounds staleness after missed invalidations.
    /// </summary>
    public TimeSpan EventTtl { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Time-to-live for the top events projection. Defaults to 1 minute:
    /// the projection is never invalidated explicitly and expires by TTL alone,
    /// so the value directly caps how stale the top list can get.
    /// </summary>
    public TimeSpan TopEventsTtl { get; set; } = TimeSpan.FromMinutes(1);
}
