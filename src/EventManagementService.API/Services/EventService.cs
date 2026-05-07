using EventManagementService.API.DataAccess;
using EventManagementService.API.Dtos;
using EventManagementService.API.Exceptions;
using EventManagementService.API.Models;
using EventManagementService.API.Validation;
using Microsoft.EntityFrameworkCore;

namespace EventManagementService.API.Services;

/// <summary>
/// EF Core implementation of <see cref="IEventService"/>.
/// </summary>
public class EventService : IEventService
{
    private readonly AppDbContext _context;

    public EventService(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public async Task<PaginatedResult<Event>> GetEventsAsync(GetEventsQuery query)
    {
        ValidateQuery(query);

        var filteredEvents = _context.Events.AsQueryable();
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

        var totalCount = await filteredEvents.CountAsync();
        var items = await filteredEvents
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArrayAsync();

        return new PaginatedResult<Event>
        {
            Items = items,
            Page = query.Page,
            Count = items.Length,
            TotalCount = totalCount
        };
    }

    /// <inheritdoc />
    public async Task<Event> GetEventByIdAsync(Guid id)
    {
        return await _context.Events.FirstOrDefaultAsync(item => item.Id == id)
            ?? throw new NotFoundException($"Событие с id {id} не найдено.");
    }

    /// <inheritdoc />
    public async Task<Event> CreateEventAsync(Event newEvent)
    {
        ArgumentNullException.ThrowIfNull(newEvent);

        await _context.Events.AddAsync(newEvent);
        await _context.SaveChangesAsync();
        return newEvent;
    }

    /// <inheritdoc />
    public async Task<Event> UpdateEventAsync(Guid id, UpdateEventRequest request)
    {
        var existingEvent = await _context.Events.FirstOrDefaultAsync(item => item.Id == id);
        if (existingEvent is null)
        {
            throw new NotFoundException($"Событие с id {id} не найдено.");
        }

        existingEvent.Update(request.Title, request.StartAt!.Value, request.EndAt!.Value, request.Description);
        await _context.SaveChangesAsync();

        return existingEvent;
    }

    /// <inheritdoc />
    public async Task DeleteEventAsync(Guid id)
    {
        var existingEvent = await _context.Events.FirstOrDefaultAsync(item => item.Id == id);
        if (existingEvent is null)
        {
            throw new NotFoundException($"Событие с id {id} не найдено.");
        }

        _context.Events.Remove(existingEvent);
        await _context.SaveChangesAsync();
    }

    private static void ValidateQuery(GetEventsQuery query)
    {
        if (query.From.HasValue && query.To.HasValue && query.From.Value > query.To.Value)
        {
            throw new BusinessValidationException("Дата начала диапазона не должна быть позже даты окончания.");
        }

        var error = GetEventsQueryValidation.Validate(query).FirstOrDefault();
        if (error is not null)
        {
            throw new BusinessValidationException(error.ErrorMessage!);
        }
    }
}
