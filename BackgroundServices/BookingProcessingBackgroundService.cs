using EventManagementService.API.Models;
using EventManagementService.API.Stores;

namespace EventManagementService.API.BackgroundServices;

/// <summary>
/// Periodically processes pending bookings in the background.
/// </summary>
public class BookingProcessingBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ProcessingDelay = TimeSpan.FromSeconds(2);

    private readonly IBookingStore _bookingStore;
    private readonly ILogger<BookingProcessingBackgroundService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BookingProcessingBackgroundService"/> class.
    /// </summary>
    /// <param name="bookingStore">Shared in-memory booking store.</param>
    /// <param name="logger">Application logger.</param>
    public BookingProcessingBackgroundService(
        IBookingStore bookingStore,
        ILogger<BookingProcessingBackgroundService> logger)
    {
        _bookingStore = bookingStore ?? throw new ArgumentNullException(nameof(bookingStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Фоновая обработка бронирований запущена.");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var pendingIds = _bookingStore.GetPendingIds();

                foreach (var bookingId in pendingIds)
                {
                    try
                    {
                        await ProcessBookingAsync(bookingId, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(exception,
                            "Ошибка при фоновой обработке бронирования с id {BookingId}.",
                            bookingId);
                    }
                }

                await Task.Delay(PollingInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Фоновая обработка бронирований остановлена.");
        }
    }

    private async Task ProcessBookingAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Начата обработка бронирования с id {BookingId}.", bookingId);

        await Task.Delay(ProcessingDelay, cancellationToken);

        var isUpdated = _bookingStore.TrySetStatus(bookingId, BookingStatus.Confirmed, DateTime.UtcNow);
        if (!isUpdated)
        {
            _logger.LogInformation(
                "Бронирование с id {BookingId} пропущено: оно уже не находится в статусе ожидания.",
                bookingId);
            return;
        }

        _logger.LogInformation("Бронирование с id {BookingId} переведено в статус Confirmed.", bookingId);
    }
}
