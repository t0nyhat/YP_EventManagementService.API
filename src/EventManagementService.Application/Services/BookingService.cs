using EventManagementService.Application.Abstractions.Repositories;
using EventManagementService.Domain.Exceptions;
using EventManagementService.Domain.Models;

namespace EventManagementService.Application.Services;

/// <summary>
/// Handles booking creation and retrieval business logic.
/// </summary>
public sealed class BookingService : IBookingService
{
    // Protects the atomic check-reserve-save sequence against concurrent booking requests.
    private static readonly SemaphoreSlim BookingLock = new(1, 1);
    private const int MaxActiveBookingsPerUser = 3;
    private readonly IEventRepository _eventRepository;
    private readonly IBookingRepository _bookingRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="BookingService"/> class.
    /// </summary>
    /// <param name="eventRepository">Event repository.</param>
    /// <param name="bookingRepository">Booking repository.</param>
    public BookingService(IEventRepository eventRepository, IBookingRepository bookingRepository)
    {
        _eventRepository = eventRepository ?? throw new ArgumentNullException(nameof(eventRepository));
        _bookingRepository = bookingRepository ?? throw new ArgumentNullException(nameof(bookingRepository));
    }

    /// <inheritdoc />
    public async Task<Booking> CreateBookingAsync(Guid eventId, Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор пользователя должен быть указан.", nameof(userId));
        }

        await BookingLock.WaitAsync();
        try
        {
            var eventItem = await _eventRepository.GetByIdAsync(eventId)
                ?? throw new NotFoundException($"Событие с id {eventId} не найдено.");

            if (eventItem.StartAt <= DateTime.UtcNow)
            {
                throw new BookingInPastException();
            }

            var activeBookings = await _bookingRepository.CountActiveByUserAsync(userId);
            if (activeBookings >= MaxActiveBookingsPerUser)
            {
                throw new TooManyActiveBookingsException(MaxActiveBookingsPerUser);
            }

            if (!eventItem.TryReserveSeats())
            {
                throw new NoAvailableSeatsException("Нет свободных мест на данное событие.");
            }

            var booking = Booking.CreatePending(eventId, userId);
            await _bookingRepository.AddAsync(booking);
            await _bookingRepository.SaveChangesAsync();

            return booking;
        }
        finally
        {
            BookingLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<Booking> GetBookingByIdAsync(Guid bookingId, Guid requesterUserId, UserRole requesterRole)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId)
            ?? throw new NotFoundException($"Бронирование с id {bookingId} не найдено.");

        EnsureAccess(booking, requesterUserId, requesterRole);
        return booking;
    }

    public async Task CancelBookingAsync(Guid bookingId, Guid requesterUserId, UserRole requesterRole)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId)
            ?? throw new NotFoundException($"Бронирование с id {bookingId} не найдено.");

        EnsureAccess(booking, requesterUserId, requesterRole);

        booking.Cancel();

        var eventItem = await _eventRepository.GetByIdAsync(booking.EventId)
            ?? throw new NotFoundException($"Событие с id {booking.EventId} не найдено.");
        eventItem.ReleaseSeats();

        await _bookingRepository.SaveChangesAsync();
    }

    private static void EnsureAccess(Booking booking, Guid requesterUserId, UserRole requesterRole)
    {
        if (requesterUserId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор пользователя должен быть указан.", nameof(requesterUserId));
        }

        if (booking.UserId != requesterUserId && requesterRole != UserRole.Admin)
        {
            throw new ForbiddenOperationException();
        }
    }
}