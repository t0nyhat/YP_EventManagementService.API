using EventManagementService.API.Models;

namespace EventManagementService.API.Services;

/// <summary>
/// Service contract for booking-related business operations.
/// </summary>
public interface IBookingService
{
    /// <summary>
    /// Creates a booking for the specified event.
    /// </summary>
    /// <param name="eventId">Event identifier.</param>
    /// <returns>The created booking.</returns>
    Task<Booking> CreateBookingAsync(Guid eventId);

    /// <summary>
    /// Gets a booking by its identifier.
    /// </summary>
    /// <param name="bookingId">Booking identifier.</param>
    /// <returns>The booking if found.</returns>
    Task<Booking> GetBookingByIdAsync(Guid bookingId);
}
