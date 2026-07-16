using Confluent.Kafka;
using Confluent.Kafka.Admin;
using EventManagementService.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventManagementService.Events.Infrastructure.Messaging;

/// <summary>
/// Initializes the required Kafka topic on startup.
/// </summary>
public sealed class KafkaTopicInitializer : IHostedService
{
    private readonly IOptions<KafkaOptions> _options;
    private readonly ILogger<KafkaTopicInitializer> _logger;

    public KafkaTopicInitializer(
        IOptions<KafkaOptions> options,
        ILogger<KafkaTopicInitializer> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var config = new AdminClientConfig
        {
            BootstrapServers = _options.Value.BootstrapServers
        };

        using var adminClient = new AdminClientBuilder(config).Build();

        try
        {
            await adminClient.CreateTopicsAsync(
                [
                    new TopicSpecification
                    {
                        Name = KafkaTopics.BookingConfirmed,
                        NumPartitions = 1,
                        ReplicationFactor = 1
                    },
                    new TopicSpecification
                    {
                        Name = KafkaTopics.BookingConfirmedDeadLetter,
                        NumPartitions = 1,
                        ReplicationFactor = 1
                    }
                ],
                new CreateTopicsOptions
                {
                    RequestTimeout = TimeSpan.FromSeconds(30)
                });

            _logger.LogInformation(
                "Kafka topics {Topic} and {DeadLetterTopic} created successfully.",
                KafkaTopics.BookingConfirmed, KafkaTopics.BookingConfirmedDeadLetter);
        }
        catch (CreateTopicsException ex) when (ex.Results.All(r =>
            r.Error.Code == ErrorCode.NoError || r.Error.Code == ErrorCode.TopicAlreadyExists))
        {
            // CreateTopicsAsync возвращает результат по каждому топику: часть может уже
            // существовать, а часть только что создана — оба исхода здесь допустимы.
            _logger.LogInformation(
                "Kafka topics ready: {Results}",
                string.Join(", ", ex.Results.Select(r => $"{r.Topic}={r.Error.Code}")));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to create Kafka topics {Topic}/{DeadLetterTopic}. Continuing.",
                KafkaTopics.BookingConfirmed, KafkaTopics.BookingConfirmedDeadLetter);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}