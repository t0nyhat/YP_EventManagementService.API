using EventManagementService.API.Models;

namespace EventManagementService.API.Services;

/// <summary>
/// In-memory implementation of <see cref="IEventService"/>.
/// Stores data only during application lifetime.
/// </summary>
public class EventService : IEventService
{
    // In-memory storage for sprint task. Data is lost after app restart.
    private readonly List<Event> _events = [];

    /// <inheritdoc />
    public IEnumerable<Event> GetAllEvents()
    {
        // Returns current in-memory collection.
        return _events;
    }

    /// <inheritdoc />
    public Event? GetEventById(Guid id)
    {
        // Returns null when event is not found to let API return 404.
        return _events.FirstOrDefault(item => item.Id == id);
    }

    /// <inheritdoc />
    public Event CreateEvent(Event newEvent)
    {
        // Always generates Id on server side and ignores client-provided Id.
        newEvent.Id = Guid.NewGuid();

        // Adds event to in-memory storage.
        _events.Add(newEvent);
        return newEvent;
    }

    /// <inheritdoc />
    public Event? UpdateEvent(Guid id, Event updatedEvent)
    {
        var existingEvent = GetEventById(id);
        if (existingEvent is null)
        {
            // Returns null when event does not exist.
            return null;
        }

        // Updates only mutable fields, keeps original Id.
        existingEvent.Title = updatedEvent.Title;
        existingEvent.Description = updatedEvent.Description;
        existingEvent.StartAt = updatedEvent.StartAt;
        existingEvent.EndAt = updatedEvent.EndAt;

        return existingEvent;
    }

    /// <inheritdoc />
    public bool DeleteEvent(Guid id)
    {
        var existingEvent = GetEventById(id);
        if (existingEvent is null)
        {
            // Returns false when there is nothing to delete.
            return false;
        }

        // True means deletion succeeded.
        return _events.Remove(existingEvent);
    }
}
