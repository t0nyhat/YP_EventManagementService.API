using Confluent.Kafka;
using EventManagementService.Contracts;
using Microsoft.Extensions.Options;

namespace EventManagementService.Bookings.Infrastructure.Messaging;

/// <summary>
/// Publishes BookingConfirmed payloads to Kafka.
/// </summary>
public sealed class KafkaBookingConfirmedPublisher : IBookingConfirmedPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;

    public KafkaBookingConfirmedPublisher(IOptions<KafkaOptions> options)
        : this(CreateProducer(options?.Value ?? throw new ArgumentNullException(nameof(options))))
    {
    }

    public KafkaBookingConfirmedPublisher(IProducer<string, string> producer)
    {
        _producer = producer ?? throw new ArgumentNullException(nameof(producer));
    }

    public Task PublishAsync(Guid eventId, string payload, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        var message = new Message<string, string>
        {
            Key = eventId.ToString("D"),
            Value = payload
        };

        return _producer.ProduceAsync(KafkaTopics.BookingConfirmed, message, cancellationToken);
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
