using EventManagementService.Bookings.Domain.Models;

namespace EventManagementService.Bookings.Application.Abstractions.Repositories;

public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(Guid bookingId, CancellationToken cancellationToken = default);

    Task<int> CountActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Guid>> GetPendingIdsAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Booking booking, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
