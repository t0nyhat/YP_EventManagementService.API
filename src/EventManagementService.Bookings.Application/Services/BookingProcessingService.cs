using System.Text.Json;
using EventManagementService.Bookings.Application.Abstractions.Repositories;
using EventManagementService.Bookings.Application.Abstractions.Services;
using EventManagementService.Bookings.Domain.Exceptions;
using EventManagementService.Bookings.Domain.Models;
using EventManagementService.Contracts;
using Microsoft.Extensions.Logging;

namespace EventManagementService.Bookings.Application.Services;

/// <summary>
/// Confirms pending bookings and creates BookingConfirmed outbox rows.
/// </summary>
public sealed class BookingProcessingService : IBookingProcessingService
{
    private const int SeatsPerBooking = 1;

    private readonly IBookingRepository _bookingRepository;
    private readonly IBookingOutboxRepository _outboxRepository;
    private readonly ILogger<BookingProcessingService> _logger;

    public BookingProcessingService(
        IBookingRepository bookingRepository,
        IBookingOutboxRepository outboxRepository,
        ILogger<BookingProcessingService> logger)
    {
        _bookingRepository = bookingRepository ?? throw new ArgumentNullException(nameof(bookingRepository));
        _outboxRepository = outboxRepository ?? throw new ArgumentNullException(nameof(outboxRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ProcessPendingBookingAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId, cancellationToken);
        if (booking is null || booking.Status != BookingStatus.Pending)
        {
            _logger.LogInformation(
                "Booking {BookingId} skipped because it is missing or no longer pending.",
                bookingId);
            return;
        }

        var confirmedAtUtc = DateTimeOffset.UtcNow;
        booking.Confirm(confirmedAtUtc.UtcDateTime);

        var message = new BookingConfirmed(
            BookingId: booking.Id,
            EventId: booking.EventId,
            UserId: booking.UserId,
            Seats: SeatsPerBooking,
            ConfirmedAtUtc: confirmedAtUtc);
        var payload = JsonSerializer.Serialize(message, KafkaJson.Options);

        await _outboxRepository.AddAsync(message, payload, confirmedAtUtc, cancellationToken);

        try
        {
            await _bookingRepository.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Пользователь успел отменить бронь между нашим чтением и записью:
            // concurrency token по статусу не дал перезаписать отмену подтверждением.
            _logger.LogInformation(
                "Booking {BookingId} was modified concurrently (likely cancelled). Confirmation skipped.",
                bookingId);
            return;
        }

        _logger.LogInformation(
            "Booking {BookingId} confirmed and BookingConfirmed outbox row created.",
            bookingId);
    }
}
