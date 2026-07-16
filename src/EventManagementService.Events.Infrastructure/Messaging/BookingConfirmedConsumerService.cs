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
    private readonly KafkaDeadLetterPublisher _deadLetterPublisher;
    private readonly ILogger<BookingConfirmedConsumerService> _logger;
    private readonly KafkaOptions _options;

    // Bounded by currently-failing offsets only: cleared on success or on dead-letter dispatch.
    private readonly Dictionary<TopicPartitionOffset, int> _attemptCounts = new();

    public BookingConfirmedConsumerService(
        IOptions<KafkaOptions> options,
        IServiceScopeFactory scopeFactory,
        KafkaDeadLetterPublisher deadLetterPublisher,
        ILogger<BookingConfirmedConsumerService> logger)
    {
        _options = options.Value;
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _deadLetterPublisher = deadLetterPublisher ?? throw new ArgumentNullException(nameof(deadLetterPublisher));
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
                        // Malformed payload cannot become valid on retry - isolate it immediately.
                        _logger.LogWarning(
                            "Failed to deserialize Kafka message at offset {Offset}. Sending to dead letter topic.",
                            result.Offset);
                        await SendToDeadLetterAsync(result, "Failed to deserialize BookingConfirmed message.", stoppingToken);
                        continue;
                    }

                    if (message.Seats <= 0)
                    {
                        _logger.LogWarning(
                            "Invalid BookingConfirmed message at offset {Offset}: Seats={Seats} must be positive. Sending to dead letter topic.",
                            result.Offset, message.Seats);
                        await SendToDeadLetterAsync(
                            result,
                            $"Invalid BookingConfirmed message: Seats={message.Seats} must be positive.",
                            stoppingToken);
                        continue;
                    }

                    try
                    {
                        await handler.HandleAsync(message, stoppingToken);
                        _attemptCounts.Remove(result.TopicPartitionOffset);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        var attempts = _attemptCounts.GetValueOrDefault(result.TopicPartitionOffset) + 1;
                        _attemptCounts[result.TopicPartitionOffset] = attempts;

                        if (attempts >= _options.MaxHandlerAttempts)
                        {
                            // Retries exhausted: a transient failure that persisted this long is
                            // treated as permanent - isolate the message instead of blocking the partition forever.
                            _logger.LogError(
                                exception,
                                "Failed to handle BookingConfirmed at offset {Offset} after {Attempts} attempts. Sending to dead letter topic.",
                                result.TopicPartitionOffset, attempts);
                            await SendToDeadLetterAsync(result, exception.Message, stoppingToken);
                            _attemptCounts.Remove(result.TopicPartitionOffset);
                            continue;
                        }

                        _logger.LogError(
                            exception,
                            "Failed to handle BookingConfirmed at offset {Offset} (attempt {Attempts}/{MaxAttempts}). Seeking back to retry.",
                            result.TopicPartitionOffset, attempts, _options.MaxHandlerAttempts);
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

    private async Task SendToDeadLetterAsync(
        ConsumeResult<string, string> result,
        string errorReason,
        CancellationToken cancellationToken)
    {
        try
        {
            await _deadLetterPublisher.PublishAsync(
                result.Message.Key,
                result.Message.Value,
                errorReason,
                result.TopicPartitionOffset,
                cancellationToken);
        }
        catch (Exception publishException) when (publishException is not OperationCanceledException)
        {
            // Publishing to the dead letter topic itself failed (e.g. Kafka is briefly
            // unreachable): do NOT commit - at-least-once will retry this whole branch
            // on the next iteration instead of silently losing the message.
            _logger.LogError(
                publishException,
                "Failed to publish message at offset {Offset} to the dead letter topic. Offset will not be committed; will retry.",
                result.TopicPartitionOffset);
            return;
        }

        _consumer.Commit(result);
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
