using System.Text.Json;
using Confluent.Kafka;
using EventManagementService.Contracts;
using EventManagementService.Events.Application.Abstractions.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventManagementService.Events.Infrastructure.Messaging;

/// <summary>
/// Background service that consumes BookingConfirmed messages from Kafka.
/// </summary>
public sealed class BookingConfirmedConsumerService : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingConfirmedConsumerService> _logger;
    private readonly KafkaOptions _options;

    public BookingConfirmedConsumerService(
        IOptions<KafkaOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<BookingConfirmedConsumerService> logger)
    {
        _options = options.Value;
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.ConsumerGroup,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        await Task.Yield();

        _consumer.Subscribe(KafkaTopics.BookingConfirmed);

        _logger.LogInformation(
            "Kafka consumer started. Subscribed to topic {Topic}. Group: {Group}.",
            KafkaTopics.BookingConfirmed, _options.ConsumerGroup);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = _consumer.Consume(stoppingToken);

                    using var scope = _scopeFactory.CreateScope();
                    var handler = scope.ServiceProvider.GetRequiredService<IBookingConfirmedHandler>();

                    var message = DeserializeMessage(result.Message.Value);

                    if (message is null)
                    {
                        _logger.LogWarning(
                            "Failed to deserialize Kafka message at offset {Offset}. Skipping.",
                            result.Offset);
                        _consumer.Commit(result);
                        continue;
                    }

                    if (message.Seats <= 0)
                    {
                        _logger.LogWarning(
                            "Invalid BookingConfirmed message at offset {Offset}: Seats={Seats} must be positive. Skipping.",
                            result.Offset, message.Seats);
                        _consumer.Commit(result);
                        continue;
                    }

                    try
                    {
                        await handler.HandleAsync(message, stoppingToken);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        _logger.LogError(
                            exception,
                            "Failed to handle BookingConfirmed at offset {Offset}. Seeking back to retry.",
                            result.TopicPartitionOffset);
                        _consumer.Seek(result.TopicPartitionOffset);
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                        continue;
                    }

                    _consumer.Commit(result);
                }
                catch (ConsumeException ex) when (ex.Error.IsLocalError)
                {
                    _logger.LogError(ex, "Kafka consume error. Waiting before retry.");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error processing Kafka message.");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }
        finally
        {
            _consumer.Close();
            _consumer.Dispose();
        }
    }

    private static BookingConfirmed? DeserializeMessage(string value)
    {
        try
        {
            return JsonSerializer.Deserialize<BookingConfirmed>(value, KafkaJson.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
