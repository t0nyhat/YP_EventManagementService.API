namespace EventManagementService.Events.Infrastructure.Messaging;

public class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "localhost:29092";
    public string ConsumerGroup { get; set; } = "events-service";
}