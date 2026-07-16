using EventManagementService.Events.Application.Abstractions.Repositories;
using EventManagementService.Events.Application.Dtos;
using EventManagementService.Events.Domain.Models;
using EventManagementService.Events.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace EventManagementService.Events.Infrastructure.Repositories;

public sealed class EventRepository : IEventRepository
{
    private readonly EventsDbContext _context;

    public EventRepository(EventsDbContext context)
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

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<Event>> GetTopEventsAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        // Ranking is computed entirely in SQL. The cast to double keeps the division
        // fractional: PostgreSQL int / int truncates (5/10 -> 0) and would break the order.
        // TotalSeats <= 0 is impossible per the domain invariant, but the conditional
        // defensively ranks such rows with ratio 0 instead of failing with division by zero.
        return await _context.Events
            .AsNoTracking()
            .OrderByDescending(eventItem => eventItem.TotalSeats > 0
                ? (double)(eventItem.TotalSeats - eventItem.AvailableSeats) / eventItem.TotalSeats
                : 0.0)
            .ThenByDescending(eventItem => eventItem.TotalSeats - eventItem.AvailableSeats)
            .ThenBy(eventItem => eventItem.StartAt)
            .ThenBy(eventItem => eventItem.Id)
            .Take(count)
            .ToListAsync(cancellationToken);
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