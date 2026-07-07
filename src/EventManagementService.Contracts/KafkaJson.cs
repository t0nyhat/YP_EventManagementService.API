using System.Text.Json;

namespace EventManagementService.Contracts;

/// <summary>
/// Shared serialization settings for Kafka messages.
/// The producer (Bookings) and the consumer (Events) must use the same instance
/// so the wire format cannot drift between services.
/// </summary>
public static class KafkaJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}
