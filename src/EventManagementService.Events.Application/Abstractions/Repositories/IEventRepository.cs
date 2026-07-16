using EventManagementService.Events.Application.Dtos;
using EventManagementService.Events.Domain.Models;

namespace EventManagementService.Events.Application.Abstractions.Repositories;

public interface IEventRepository
{
    Task<PaginatedResult<Event>> GetEventsAsync(GetEventsQuery query, CancellationToken cancellationToken = default);

    Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Event eventItem, CancellationToken cancellationToken = default);

    void Remove(Event eventItem);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}