using EventManagementService.API.Dtos;
using EventManagementService.API.Exceptions;
using EventManagementService.API.Models;
using EventManagementService.API.Validation;

namespace EventManagementService.API.Services;

/// <summary>
/// In-memory implementation of <see cref="IEventService"/>.
/// Stores data only during application lifetime.
/// </summary>
public class EventService : IEventService
{
    // In-memory storage for sprint task. Data is lost after app restart.
    private readonly List<Event> _events = [];
    
    // Lock object to protect concurrent access to _events collection.
    private readonly object _lock = new object();

    /// <inheritdoc />
    public PaginatedResult<Event> GetEvents(GetEventsQuery query)
    {
        ValidateQuery(query);

        List<Event> snapshot;
        lock (_lock)
        {
            snapshot = _events.ToList();
        }

        var filteredEvents = snapshot.AsEnumerable();
        var normalizedTitle = query.Title?.Trim();

        if (!string.IsNullOrWhiteSpace(normalizedTitle))
        {
            filteredEvents = filteredEvents.Where(eventItem =>
                eventItem.Title.Contains(normalizedTitle, StringComparison.OrdinalIgnoreCase));
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
        lock (_lock)
        {
            return _events.FirstOrDefault(item => item.Id == id)
                ?? throw new NotFoundException($"Событие с id {id} не найдено.");
        }
    }

    /// <inheritdoc />
    public Event CreateEvent(Event newEvent)
    {
        lock (_lock)
        {
            _events.Add(newEvent);
            return newEvent;
        }
    }

    /// <inheritdoc />
    public Event UpdateEvent(Guid id, UpdateEventRequest request)
    {
        lock (_lock)
        {
            var existingEvent = _events.FirstOrDefault(item => item.Id == id);
            if (existingEvent is null)
            {
                throw new NotFoundException($"Событие с id {id} не найдено.");
            }

            existingEvent.Update(request.Title, request.StartAt!.Value, request.EndAt!.Value, request.Description);

            return existingEvent;
        }
    }

    /// <inheritdoc />
    public void DeleteEvent(Guid id)
    {
        lock (_lock)
        {
            var existingEvent = _events.FirstOrDefault(item => item.Id == id);
            if (existingEvent is null)
            {
                throw new NotFoundException($"Событие с id {id} не найдено.");
            }

            _events.Remove(existingEvent);
        }
    }

    /// <inheritdoc />
    public bool TryReserveSeats(Guid eventId)
    {
        lock (_lock)
        {
            var eventItem = _events.FirstOrDefault(item => item.Id == eventId)
                ?? throw new NotFoundException($"Событие с id {eventId} не найдено.");

            return eventItem.TryReserveSeats();
        }
    }

    /// <inheritdoc />
    public void ReleaseSeats(Guid eventId)
    {
        lock (_lock)
        {
            var eventItem = _events.FirstOrDefault(item => item.Id == eventId);
            eventItem?.ReleaseSeats();
        }
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
