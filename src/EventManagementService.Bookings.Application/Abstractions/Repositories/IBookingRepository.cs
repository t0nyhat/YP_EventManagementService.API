using EventManagementService.Bookings.Domain.Models;

namespace EventManagementService.Bookings.Application.Abstractions.Repositories;

public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(Guid bookingId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Guid>> GetPendingIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically checks the user's active booking limit and saves the new booking.
    /// Throws <see cref="Domain.Exceptions.TooManyActiveBookingsException"/> when the limit is exceeded.
    /// </summary>
    Task AddWithActiveLimitAsync(
        Booking booking,
        int maxActiveBookingsPerUser,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reloads the booking state from the database (to retry after a concurrency conflict).
    /// </summary>
    Task ReloadAsync(Booking booking, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
