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

        // Ранжирование целиком считается в SQL. Приведение к double сохраняет деление
        // дробным: в PostgreSQL int / int усекается (5/10 -> 0) и порядок бы сломался.
        // TotalSeats <= 0 невозможен по доменному инварианту, но условие защитно
        // даёт таким строкам коэффициент 0 вместо падения с делением на ноль.
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