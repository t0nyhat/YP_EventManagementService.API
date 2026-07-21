using System.Text.Json;

namespace EventManagementService.Events.Infrastructure.Caching;

/// <summary>
/// Shared serialization settings for Redis cache payloads.
/// All cache readers and writers must use these options so the stored JSON format
/// can be evolved in one place if the API response contract changes.
/// </summary>
public static class CacheJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}
