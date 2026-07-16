namespace EventManagementService.Events.Infrastructure.Messaging;

public class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "localhost:29092";
    public string ConsumerGroup { get; set; } = "events-service";

    /// <summary>
    /// Number of handler attempts (initial + retries via Seek) before a message
    /// is routed to the Dead Letter Topic instead of being retried again.
    /// </summary>
    public int MaxHandlerAttempts { get; set; } = 5;
}