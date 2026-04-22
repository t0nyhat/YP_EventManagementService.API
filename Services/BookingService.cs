using EventManagementService.API.Exceptions;
using EventManagementService.API.Models;
using EventManagementService.API.Stores;

namespace EventManagementService.API.Services;

/// <summary>
/// Handles booking creation and retrieval business logic.
/// </summary>
public class BookingService : IBookingService
{
    private readonly IBookingStore _bookingStore;
    private readonly IEventService _eventService;

    // Protects the atomic check-reserve-save sequence against concurrent booking requests.
    private readonly object _bookingLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="BookingService"/> class.
    /// </summary>
    /// <param name="bookingStore">Booking storage shared with background processing.</param>
    /// <param name="eventService">Event service used to verify event existence and manage seats.</param>
    public BookingService(IBookingStore bookingStore, IEventService eventService)
    {
        _bookingStore = bookingStore ?? throw new ArgumentNullException(nameof(bookingStore));
        _eventService = eventService ?? throw new ArgumentNullException(nameof(eventService));
    }

    /// <inheritdoc />
    public Task<Booking> CreateBookingAsync(Guid eventId)
    {
        lock (_bookingLock)
        {
            // Throws NotFoundException if event does not exist.
            var reserved = _eventService.TryReserveSeats(eventId);

            if (!reserved)
            {
                throw new NoAvailableSeatsException("Нет свободных мест на данное событие.");
            }

            var booking = Booking.CreatePending(eventId);
            var storedBooking = _bookingStore.Add(booking);

            return Task.FromResult(storedBooking);
        }
    }

    /// <inheritdoc />
    public Task<Booking> GetBookingByIdAsync(Guid bookingId)
    {
        var booking = _bookingStore.GetById(bookingId)
            ?? throw new NotFoundException($"Бронирование с id {bookingId} не найдено.");

        return Task.FromResult(booking);
    }
}
