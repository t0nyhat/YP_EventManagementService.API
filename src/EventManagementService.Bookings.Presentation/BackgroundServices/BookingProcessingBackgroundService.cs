using EventManagementService.Bookings.Application.Abstractions.Repositories;
using EventManagementService.Bookings.Application.Abstractions.Services;

namespace EventManagementService.Bookings.Presentation.BackgroundServices;

/// <summary>
/// Polls pending bookings and asks the application service to confirm them.
/// </summary>
public sealed class BookingProcessingBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ProcessingDelay = TimeSpan.FromSeconds(2);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingProcessingBackgroundService> _logger;

    public BookingProcessingBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<BookingProcessingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Booking processing background service started.");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    IReadOnlyCollection<Guid> pendingIds;

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var bookingRepository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
                        pendingIds = await bookingRepository.GetPendingIdsAsync(stoppingToken);
                    }

                    if (pendingIds.Count > 0)
                    {
                        var tasks = pendingIds.Select(id => ProcessBookingAsync(id, stoppingToken));
                        await Task.WhenAll(tasks);
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // Транзиентная ошибка (например, недоступна БД) не должна ронять хост:
                    // необработанное исключение из ExecuteAsync останавливает всё приложение.
                    _logger.LogError(exception, "Booking processing polling iteration failed. Will retry on next tick.");
                }

                await Task.Delay(PollingInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Booking processing background service stopped.");
        }
    }

    private async Task ProcessBookingAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(ProcessingDelay, cancellationToken);

            using var scope = _scopeFactory.CreateScope();
            var processingService = scope.ServiceProvider.GetRequiredService<IBookingProcessingService>();
            await processingService.ProcessPendingBookingAsync(bookingId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to process booking {BookingId}.", bookingId);
        }
    }
}
