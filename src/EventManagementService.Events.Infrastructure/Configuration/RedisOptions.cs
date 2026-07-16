namespace EventManagementService.Events.Infrastructure.Configuration;

/// <summary>
/// Redis connection settings, bound from the "Redis" configuration section.
/// Binding and validation are performed in Infrastructure DI:
/// the connection string must be non-empty at startup.
/// </summary>
public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    /// <summary>
    /// StackExchange.Redis connection string, for example <c>localhost:6379</c>.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;
}
