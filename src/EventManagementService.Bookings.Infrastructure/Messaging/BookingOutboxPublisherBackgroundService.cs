using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventManagementService.Bookings.Infrastructure.Messaging;

/// <summary>
/// Background worker that retries unpublished outbox rows.
/// </summary>
public sealed class BookingOutboxPublisherBackgroundService : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan PurgeInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan PublishedRetention = TimeSpan.FromDays(7);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingOutboxPublisherBackgroundService> _logger;

    public BookingOutboxPublisherBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<BookingOutboxPublisherBackgroundService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Booking outbox publisher background service started.");

        try
        {
            var nextPurgeAtUtc = DateTimeOffset.UtcNow;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var publisher = scope.ServiceProvider.GetRequiredService<BookingOutboxPublisher>();
                    await publisher.PublishPendingBatchAsync(BatchSize, stoppingToken);

                    if (DateTimeOffset.UtcNow >= nextPurgeAtUtc)
                    {
                        await publisher.PurgePublishedAsync(PublishedRetention, stoppingToken);
                        nextPurgeAtUtc = DateTimeOffset.UtcNow.Add(PurgeInterval);
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // Транзиентная ошибка (например, недоступна БД) не должна ронять хост:
                    // необработанное исключение из ExecuteAsync останавливает всё приложение.
                    _logger.LogError(exception, "Outbox publishing iteration failed. Will retry on next tick.");
                }

                await Task.Delay(PollingInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Booking outbox publisher background service stopped.");
        }
    }
}
