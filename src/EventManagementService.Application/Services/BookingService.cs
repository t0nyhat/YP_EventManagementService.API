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
    public async Task<Booking> CreateBookingAsync(Guid eventId)
    {
        await BookingLock.WaitAsync();
        try
        {
            var eventItem = await _eventRepository.GetByIdAsync(eventId)
                ?? throw new NotFoundException($"Событие с id {eventId} не найдено.");

            if (!eventItem.TryReserveSeats())
            {
                throw new NoAvailableSeatsException("Нет свободных мест на данное событие.");
            }

            var booking = Booking.CreatePending(eventId);
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
    public async Task<Booking> GetBookingByIdAsync(Guid bookingId)
    {
        return await _bookingRepository.GetByIdAsync(bookingId)
            ?? throw new NotFoundException($"Бронирование с id {bookingId} не найдено.");
    }
}