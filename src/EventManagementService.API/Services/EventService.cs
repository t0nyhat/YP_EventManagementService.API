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
    public PaginatedResult<Event> GetEvents(GetEventsQuery query)
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

        var totalCount = filteredEvents.Count();
        var items = filteredEvents
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArray();

        return new PaginatedResult<Event>
        {
            Items = items,
            Page = query.Page,
            Count = items.Length,
            TotalCount = totalCount
        };
    }

    /// <inheritdoc />
    public Event GetEventById(Guid id)
    {
        return _context.Events.FirstOrDefault(item => item.Id == id)
            ?? throw new NotFoundException($"Событие с id {id} не найдено.");
    }

    /// <inheritdoc />
    public Event CreateEvent(Event newEvent)
    {
        ArgumentNullException.ThrowIfNull(newEvent);

        _context.Events.Add(newEvent);
        _context.SaveChanges();
        return newEvent;
    }

    /// <inheritdoc />
    public Event UpdateEvent(Guid id, UpdateEventRequest request)
    {
        var existingEvent = _context.Events.FirstOrDefault(item => item.Id == id);
        if (existingEvent is null)
        {
            throw new NotFoundException($"Событие с id {id} не найдено.");
        }

        existingEvent.Update(request.Title, request.StartAt!.Value, request.EndAt!.Value, request.Description);
        _context.SaveChanges();

        return existingEvent;
    }

    /// <inheritdoc />
    public void DeleteEvent(Guid id)
    {
        var existingEvent = _context.Events.FirstOrDefault(item => item.Id == id);
        if (existingEvent is null)
        {
            throw new NotFoundException($"Событие с id {id} не найдено.");
        }

        _context.Events.Remove(existingEvent);
        _context.SaveChanges();
    }

    /// <inheritdoc />
    public bool TryReserveSeats(Guid eventId)
    {
        var eventItem = _context.Events.FirstOrDefault(item => item.Id == eventId)
            ?? throw new NotFoundException($"Событие с id {eventId} не найдено.");

        var reserved = eventItem.TryReserveSeats();
        if (reserved)
        {
            _context.SaveChanges();
        }

        return reserved;
    }

    /// <inheritdoc />
    public void ReleaseSeats(Guid eventId)
    {
        var eventItem = _context.Events.FirstOrDefault(item => item.Id == eventId);
        if (eventItem is null)
        {
            return;
        }

        eventItem.ReleaseSeats();
        _context.SaveChanges();
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
