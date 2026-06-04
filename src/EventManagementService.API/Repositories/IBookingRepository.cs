using EventManagementService.Domain.Models;

namespace EventManagementService.API.Repositories;

public interface IBookingRepository
{
    Task<Booking?> GetByIdAsync(Guid bookingId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Guid>> GetPendingIdsAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Booking booking, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
