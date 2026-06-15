using EventManagementService.Domain.Models;

namespace EventManagementService.Application.Services;

/// <summary>
/// Service contract for booking-related business operations.
/// </summary>
public interface IBookingService
{
    /// <summary>
    /// Creates a booking for the specified event.
    /// </summary>
    /// <param name="eventId">Event identifier.</param>
    /// <param name="userId">Current user identifier.</param>
    /// <returns>The created booking.</returns>
    Task<Booking> CreateBookingAsync(Guid eventId, Guid userId);

    /// <summary>
    /// Gets a booking by its identifier.
    /// </summary>
    /// <param name="bookingId">Booking identifier.</param>
    /// <param name="requesterUserId">Current user identifier.</param>
    /// <param name="requesterRole">Current user role.</param>
    /// <returns>The booking if found.</returns>
    Task<Booking> GetBookingByIdAsync(Guid bookingId, Guid requesterUserId, UserRole requesterRole);

    /// <summary>
    /// Cancels a booking if the requester is its owner or an administrator.
    /// </summary>
    /// <param name="bookingId">Booking identifier.</param>
    /// <param name="requesterUserId">Current user identifier.</param>
    /// <param name="requesterRole">Current user role.</param>
    Task CancelBookingAsync(Guid bookingId, Guid requesterUserId, UserRole requesterRole);
}