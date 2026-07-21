using EventManagementService.Events.Application.Dtos;
using EventManagementService.Events.Domain.Models;

namespace EventManagementService.Events.Application.Abstractions.Repositories;

public interface IEventRepository
{
    Task<PaginatedResult<Event>> GetEventsAsync(GetEventsQuery query, CancellationToken cancellationToken = default);

    Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the top events ordered by the sold-seat ratio
    /// (TotalSeats - AvailableSeats) / TotalSeats, descending.
    /// The order is deterministic: ties are broken by the number of sold seats descending,
    /// then by <see cref="Event.StartAt"/> ascending, then by <see cref="Event.Id"/> ascending.
    /// An event with a non-positive TotalSeats (impossible per domain invariant, defensive)
    /// participates with ratio 0 instead of being filtered out or causing division by zero.
    /// </summary>
    /// <param name="count">Maximum number of events to return; must be positive.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>At most <paramref name="count"/> events; an empty collection when there are none.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is not positive.</exception>
    Task<IReadOnlyCollection<Event>> GetTopEventsAsync(
        int count,
        CancellationToken cancellationToken = default);

    Task AddAsync(Event eventItem, CancellationToken cancellationToken = default);

    void Remove(Event eventItem);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}