using EventManagementService.API.DataAccess;
using EventManagementService.API.Exceptions;
using EventManagementService.API.Models;
using Microsoft.EntityFrameworkCore;

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
                    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    pendingIds = await context.Bookings
                        .Where(booking => booking.Status == BookingStatus.Pending)
                        .Select(booking => booking.Id)
                        .ToListAsync(stoppingToken);
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

        // Processing delay runs outside the semaphore so all bookings delay in parallel.
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
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var booking = await context.Bookings.FirstOrDefaultAsync(item => item.Id == bookingId, cancellationToken);
            if (booking is null || booking.Status != BookingStatus.Pending)
            {
                _logger.LogInformation(
                    "Бронирование с id {BookingId} пропущено: оно уже не находится в статусе ожидания.",
                    bookingId);
                return;
            }

            var eventItem = await context.Events.FirstOrDefaultAsync(item => item.Id == booking.EventId, cancellationToken);
            if (eventItem is null)
            {
                booking.Reject(DateTime.UtcNow);
                await context.SaveChangesAsync(cancellationToken);
                _logger.LogWarning(
                    "Событие для бронирования с id {BookingId} удалено. Бронирование отклонено.",
                    bookingId);
                return;
            }

            booking.Confirm(DateTime.UtcNow);
            await context.SaveChangesAsync(cancellationToken);
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
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var booking = await context.Bookings.FirstOrDefaultAsync(item => item.Id == bookingId, cancellationToken);

            if (booking is not null && booking.Status == BookingStatus.Pending)
            {
                booking.Reject(DateTime.UtcNow);

                var eventItem = await context.Events.FirstOrDefaultAsync(item => item.Id == booking.EventId, cancellationToken);
                eventItem?.ReleaseSeats();

                await context.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
