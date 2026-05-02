using EventManagementService.API.Models;

namespace EventManagementService.API.Stores;

/// <summary>
/// In-memory storage contract for bookings shared by API services and background processing.
/// </summary>
public interface IBookingStore
{
    /// <summary>
    /// Adds a booking to the store.
    /// </summary>
    /// <param name="booking">The booking to store.</param>
    /// <returns>A detached copy of the stored booking.</returns>
    Booking Add(Booking booking);

    /// <summary>
    /// Retrieves a booking by its identifier.
    /// </summary>
    /// <param name="id">Booking identifier.</param>
    /// <returns>A detached copy of the booking if found; otherwise <c>null</c>.</returns>
    Booking? GetById(Guid id);

    /// <summary>
    /// Returns a snapshot of booking identifiers still waiting for processing.
    /// </summary>
    /// <returns>Pending booking identifiers.</returns>
    IReadOnlyCollection<Guid> GetPendingIds();

    /// <summary>
    /// Updates the status of a pending booking and stores its processing timestamp.
    /// </summary>
    /// <param name="bookingId">Booking identifier.</param>
    /// <param name="status">Target terminal status.</param>
    /// <param name="processedAt">Processing timestamp.</param>
    /// <returns><c>true</c> if the status was changed; otherwise <c>false</c>.</returns>
    bool TrySetStatus(Guid bookingId, BookingStatus status, DateTime processedAt);
}
