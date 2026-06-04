using EventManagementService.Application.Abstractions.Repositories;
using EventManagementService.Domain.Models;
using Microsoft.Extensions.Logging;

namespace EventManagementService.Application.Services;

/// <summary>
/// Processes a single pending booking by applying business rules:
/// - skip if not pending;
/// - reject if event deleted;
/// - confirm on success;
/// - on exception, reject and release seat if event exists.
/// </summary>
public sealed class BookingProcessingService : IBookingProcessingService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IEventRepository _eventRepository;
    private readonly ILogger<BookingProcessingService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="BookingProcessingService"/> class.
    /// </summary>
    /// <param name="bookingRepository">Booking repository.</param>
    /// <param name="eventRepository">Event repository.</param>
    /// <param name="logger">Application logger.</param>
    public BookingProcessingService(
        IBookingRepository bookingRepository,
        IEventRepository eventRepository,
        ILogger<BookingProcessingService> logger)
    {
        _bookingRepository = bookingRepository ?? throw new ArgumentNullException(nameof(bookingRepository));
        _eventRepository = eventRepository ?? throw new ArgumentNullException(nameof(eventRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task ProcessPendingBookingAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Начата обработка бронирования с id {BookingId}.", bookingId);

        try
        {
            var booking = await _bookingRepository.GetByIdAsync(bookingId, cancellationToken);
            if (booking is null || booking.Status != BookingStatus.Pending)
            {
                _logger.LogInformation(
                    "Бронирование с id {BookingId} пропущено: оно уже не находится в статусе ожидания.",
                    bookingId);
                return;
            }

            var eventItem = await _eventRepository.GetByIdAsync(booking.EventId, cancellationToken);
            if (eventItem is null)
            {
                booking.Reject(DateTime.UtcNow);
                await _bookingRepository.SaveChangesAsync(cancellationToken);
                _logger.LogWarning(
                    "Событие для бронирования с id {BookingId} удалено. Бронирование отклонено.",
                    bookingId);
                return;
            }

            booking.Confirm(DateTime.UtcNow);
            await _bookingRepository.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Бронирование с id {BookingId} переведено в статус Confirmed.", bookingId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Ошибка при фоновой обработке бронирования с id {BookingId}.",
                bookingId);

            var booking = await _bookingRepository.GetByIdAsync(bookingId, cancellationToken);
            if (booking is not null && booking.Status == BookingStatus.Pending)
            {
                booking.Reject(DateTime.UtcNow);

                var eventItem = await _eventRepository.GetByIdAsync(booking.EventId, cancellationToken);
                eventItem?.ReleaseSeats();

                await _bookingRepository.SaveChangesAsync(cancellationToken);
            }
        }
    }
}