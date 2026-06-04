using EventManagementService.API.Dtos;
using EventManagementService.Domain.Models;

namespace EventManagementService.API.Repositories;

public interface IEventRepository
{
    Task<PaginatedResult<Event>> GetEventsAsync(GetEventsQuery query, CancellationToken cancellationToken = default);

    Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Event eventItem, CancellationToken cancellationToken = default);

    void Remove(Event eventItem);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
