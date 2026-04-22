using EventManagementService.API.Exceptions;
using EventManagementService.API.Models;
using EventManagementService.API.Services;
using EventManagementService.API.Stores;

namespace EventManagementService.API.BackgroundServices;

/// <summary>
/// Periodically processes pending bookings in the background.
/// Pending bookings are dispatched in parallel via Task.WhenAll.
/// Write operations (status updates) are serialized through a SemaphoreSlim.
/// </summary>
public class BookingProcessingBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ProcessingDelay = TimeSpan.FromSeconds(2);

    private readonly IBookingStore _bookingStore;
    private readonly IEventService _eventService;
    private readonly ILogger<BookingProcessingBackgroundService> _logger;

    // Serializes write operations (status updates) while allowing delays to run in parallel.
    private readonly SemaphoreSlim _processingSemaphore = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="BookingProcessingBackgroundService"/> class.
    /// </summary>
    /// <param name="bookingStore">Shared in-memory booking store.</param>
    /// <param name="eventService">Event service used to verify event existence and release seats.</param>
    /// <param name="logger">Application logger.</param>
    public BookingProcessingBackgroundService(
        IBookingStore bookingStore,
        IEventService eventService,
        ILogger<BookingProcessingBackgroundService> logger)
    {
        _bookingStore = bookingStore ?? throw new ArgumentNullException(nameof(bookingStore));
        _eventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
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

                if (pendingIds.Count > 0)
                {
                    // Delays for all bookings run in parallel; writes are serialized inside ProcessBookingAsync.
                    var tasks = pendingIds.Select(id => ProcessBookingAsync(id, stoppingToken));
                    await Task.WhenAll(tasks);
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

        // Processing delay runs outside the semaphore so all bookings delay in parallel.
        try
        {
            await Task.Delay(ProcessingDelay, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }

        var semaphoreAcquired = false;
        try
        {
            await _processingSemaphore.WaitAsync(cancellationToken);
            semaphoreAcquired = true;

            var booking = _bookingStore.GetById(bookingId);
            if (booking is null || booking.Status != BookingStatus.Pending)
            {
                _logger.LogInformation(
                    "Бронирование с id {BookingId} пропущено: оно уже не находится в статусе ожидания.",
                    bookingId);
                return;
            }

            Event? eventItem = null;
            try
            {
                eventItem = _eventService.GetEventById(booking.EventId);
            }
            catch (NotFoundException) { }

            if (eventItem is null)
            {
                _bookingStore.TrySetStatus(bookingId, BookingStatus.Rejected, DateTime.UtcNow);
                _logger.LogWarning(
                    "Событие для бронирования с id {BookingId} удалено. Бронирование отклонено.",
                    bookingId);
                return;
            }

            _bookingStore.TrySetStatus(bookingId, BookingStatus.Confirmed, DateTime.UtcNow);
            _logger.LogInformation("Бронирование с id {BookingId} переведено в статус Confirmed.", bookingId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Ошибка при фоновой обработке бронирования с id {BookingId}.",
                bookingId);

            var booking = _bookingStore.GetById(bookingId);
            if (booking is not null && booking.Status == BookingStatus.Pending)
            {
                _bookingStore.TrySetStatus(bookingId, BookingStatus.Rejected, DateTime.UtcNow);
                _eventService.ReleaseSeats(booking.EventId);
            }
        }
        finally
        {
            if (semaphoreAcquired)
            {
                _processingSemaphore.Release();
            }
        }
    }
}
