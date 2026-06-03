using EventManagementService.API.DataAccess;
using EventManagementService.API.Dtos;
using EventManagementService.API.Models;
using Microsoft.EntityFrameworkCore;

namespace EventManagementService.API.Repositories;

internal sealed class EventRepository : IEventRepository
{
    private readonly AppDbContext _context;

    public EventRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<PaginatedResult<Event>> GetEventsAsync(GetEventsQuery query, CancellationToken cancellationToken = default)
    {
        var filteredEvents = _context.Events
            .AsNoTracking()
            .AsQueryable();
        var normalizedTitle = query.Title?.Trim();

        if (!string.IsNullOrWhiteSpace(normalizedTitle))
        {
            filteredEvents = filteredEvents.Where(eventItem =>
                eventItem.Title.ToLower().Contains(normalizedTitle.ToLower()));
        }

        if (query.From.HasValue)
        {
            filteredEvents = filteredEvents.Where(eventItem => eventItem.StartAt >= query.From.Value);
        }

        if (query.To.HasValue)
        {
            filteredEvents = filteredEvents.Where(eventItem => eventItem.EndAt <= query.To.Value);
        }

        filteredEvents = filteredEvents.OrderBy(eventItem => eventItem.StartAt);

        var totalCount = await filteredEvents.CountAsync(cancellationToken);
        var items = await filteredEvents
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArrayAsync(cancellationToken);

        return new PaginatedResult<Event>
        {
            Items = items,
            Page = query.Page,
            Count = items.Length,
            TotalCount = totalCount
        };
    }

    public Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.Events.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public Task AddAsync(Event eventItem, CancellationToken cancellationToken = default)
    {
        return _context.Events.AddAsync(eventItem, cancellationToken).AsTask();
    }

    public void Remove(Event eventItem)
    {
        _context.Events.Remove(eventItem);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
