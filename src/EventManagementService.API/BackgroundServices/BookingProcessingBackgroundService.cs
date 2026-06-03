using EventManagementService.API.Exceptions;
using EventManagementService.API.Models;
using EventManagementService.API.Repositories;

namespace EventManagementService.API.BackgroundServices;

/// <summary>
/// Periodically processes pending bookings in the background.
/// Pending bookings are dispatched in parallel via Task.WhenAll.
/// </summary>
public class BookingProcessingBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ProcessingDelay = TimeSpan.FromSeconds(2);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingProcessingBackgroundService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BookingProcessingBackgroundService"/> class.
    /// </summary>
    /// <param name="scopeFactory">Factory used to create scoped services for each processing cycle.</param>
    /// <param name="logger">Application logger.</param>
    public BookingProcessingBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<BookingProcessingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
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
                List<Guid> pendingIds;

                using (var scope = _scopeFactory.CreateScope())
                {
                    var bookingRepository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
                    pendingIds = (await bookingRepository.GetPendingIdsAsync(stoppingToken)).ToList();
                }

                if (pendingIds.Count > 0)
                {
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

        // Processing delay runs before scoped processing so all bookings delay in parallel.
        try
        {
            await Task.Delay(ProcessingDelay, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var bookingRepository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
            var eventRepository = scope.ServiceProvider.GetRequiredService<IEventRepository>();

            var booking = await bookingRepository.GetByIdAsync(bookingId, cancellationToken);
            if (booking is null || booking.Status != BookingStatus.Pending)
            {
                _logger.LogInformation(
                    "Бронирование с id {BookingId} пропущено: оно уже не находится в статусе ожидания.",
                    bookingId);
                return;
            }

            var eventItem = await eventRepository.GetByIdAsync(booking.EventId, cancellationToken);
            if (eventItem is null)
            {
                booking.Reject(DateTime.UtcNow);
                await bookingRepository.SaveChangesAsync(cancellationToken);
                _logger.LogWarning(
                    "Событие для бронирования с id {BookingId} удалено. Бронирование отклонено.",
                    bookingId);
                return;
            }

            booking.Confirm(DateTime.UtcNow);
            await bookingRepository.SaveChangesAsync(cancellationToken);
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

            using var scope = _scopeFactory.CreateScope();
            var bookingRepository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
            var eventRepository = scope.ServiceProvider.GetRequiredService<IEventRepository>();
            var booking = await bookingRepository.GetByIdAsync(bookingId, cancellationToken);

            if (booking is not null && booking.Status == BookingStatus.Pending)
            {
                booking.Reject(DateTime.UtcNow);

                var eventItem = await eventRepository.GetByIdAsync(booking.EventId, cancellationToken);
                eventItem?.ReleaseSeats();

                await bookingRepository.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
