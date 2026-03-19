using EventManagementService.API.Exceptions;
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
    
    // Lock object to protect concurrent access to _events collection.
    private readonly object _lock = new object();

    /// <inheritdoc />
    public IEnumerable<Event> GetAllEvents()
    {
        lock (_lock)
        {
            // Returns a copy to prevent external modification of internal state.
            return _events.ToList();
        }
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
        ValidateEvent(newEvent);

        lock (_lock)
        {
            // Always generates Id on server side and ignores client-provided Id.
            newEvent.Id = Guid.NewGuid();

            // Adds event to in-memory storage.
            _events.Add(newEvent);
            return newEvent;
        }
    }

    /// <inheritdoc />
    public Event UpdateEvent(Guid id, Event updatedEvent)
    {
        lock (_lock)
        {
            var existingEvent = _events.FirstOrDefault(item => item.Id == id);
            if (existingEvent is null)
            {
                throw new NotFoundException($"Событие с id {id} не найдено.");
            }

            ValidateEvent(updatedEvent);

            // Updates only mutable fields, keeps original Id.
            existingEvent.Title = updatedEvent.Title;
            existingEvent.Description = updatedEvent.Description ?? existingEvent.Description;
            existingEvent.StartAt = updatedEvent.StartAt;
            existingEvent.EndAt = updatedEvent.EndAt;

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

    private static void ValidateEvent(Event eventItem)
    {
        if (string.IsNullOrWhiteSpace(eventItem.Title))
        {
            throw new BusinessValidationException("Название события не должно быть пустым.");
        }

        if (eventItem.EndAt <= eventItem.StartAt)
        {
            throw new BusinessValidationException("Дата окончания должна быть позже даты начала события.");
        }
    }
}
