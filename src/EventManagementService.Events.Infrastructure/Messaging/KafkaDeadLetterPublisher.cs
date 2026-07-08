using System.Text;
using Confluent.Kafka;
using EventManagementService.Contracts;
using Microsoft.Extensions.Options;

namespace EventManagementService.Events.Infrastructure.Messaging;

/// <summary>
/// Publishes messages the consumer could not process to the Dead Letter Topic.
/// The original payload is preserved untouched in the message value; diagnostic
/// metadata (error reason, source topic/partition/offset, timestamp) goes into headers.
/// </summary>
public sealed class KafkaDeadLetterPublisher : IDisposable
{
    private readonly IProducer<string, string> _producer;

    public KafkaDeadLetterPublisher(IOptions<KafkaOptions> options)
        : this(CreateProducer(options?.Value ?? throw new ArgumentNullException(nameof(options))))
    {
    }

    public KafkaDeadLetterPublisher(IProducer<string, string> producer)
    {
        _producer = producer ?? throw new ArgumentNullException(nameof(producer));
    }

    public Task PublishAsync(
        string key,
        string payload,
        string errorReason,
        TopicPartitionOffset source,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        ArgumentNullException.ThrowIfNull(source);

        var message = new Message<string, string>
        {
            Key = key,
            Value = payload,
            Headers =
            [
                new Header("error-reason", Encoding.UTF8.GetBytes(errorReason)),
                new Header("error-source-topic", Encoding.UTF8.GetBytes(source.Topic)),
                new Header("error-source-partition", Encoding.UTF8.GetBytes(source.Partition.Value.ToString())),
                new Header("error-source-offset", Encoding.UTF8.GetBytes(source.Offset.Value.ToString())),
                new Header("error-timestamp", Encoding.UTF8.GetBytes(DateTimeOffset.UtcNow.ToString("O")))
            ]
        };

        return _producer.ProduceAsync(KafkaTopics.BookingConfirmedDeadLetter, message, cancellationToken);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }

    private static IProducer<string, string> CreateProducer(KafkaOptions options)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = options.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true
        };

        return new ProducerBuilder<string, string>(config).Build();
    }
}
