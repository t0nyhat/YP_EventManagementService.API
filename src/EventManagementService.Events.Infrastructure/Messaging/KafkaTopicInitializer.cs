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
                    }
                ],
                new CreateTopicsOptions
                {
                    RequestTimeout = TimeSpan.FromSeconds(30)
                });

            _logger.LogInformation("Kafka topic {Topic} created successfully.", KafkaTopics.BookingConfirmed);
        }
        catch (CreateTopicsException ex) when (ex.Results.Any(r =>
            r.Error.Code == ErrorCode.TopicAlreadyExists))
        {
            _logger.LogInformation("Kafka topic {Topic} already exists.", KafkaTopics.BookingConfirmed);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create Kafka topic {Topic}. Continuing.", KafkaTopics.BookingConfirmed);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}