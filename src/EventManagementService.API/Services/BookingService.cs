using EventManagementService.API.DataAccess;
using EventManagementService.API.Exceptions;
using EventManagementService.API.Models;
using Microsoft.EntityFrameworkCore;

namespace EventManagementService.API.Services;

/// <summary>
/// Handles booking creation and retrieval business logic.
/// </summary>
public class BookingService : IBookingService
{
    // Protects the atomic check-reserve-save sequence against concurrent booking requests.
    private static readonly SemaphoreSlim BookingLock = new(1, 1);
    private readonly AppDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="BookingService"/> class.
    /// </summary>
    /// <param name="context">Database context.</param>
    public BookingService(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public async Task<Booking> CreateBookingAsync(Guid eventId)
    {
        await BookingLock.WaitAsync();
        try
        {
            var eventItem = await _context.Events.FirstOrDefaultAsync(item => item.Id == eventId)
                ?? throw new NotFoundException($"Событие с id {eventId} не найдено.");

            var reserved = eventItem.TryReserveSeats();

            if (!reserved)
            {
                throw new NoAvailableSeatsException("Нет свободных мест на данное событие.");
            }

            var booking = Booking.CreatePending(eventId);
            await _context.Bookings.AddAsync(booking);
            await _context.SaveChangesAsync();

            return booking;
        }
        finally
        {
            BookingLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<Booking> GetBookingByIdAsync(Guid bookingId)
    {
        var booking = await _context.Bookings.FirstOrDefaultAsync(item => item.Id == bookingId)
            ?? throw new NotFoundException($"Бронирование с id {bookingId} не найдено.");

        return booking;
    }
}
