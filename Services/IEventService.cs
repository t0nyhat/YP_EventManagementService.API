using EventManagementService.API.Models;

namespace EventManagementService.API.Services;

/// <summary>
/// Service for managing event-related operations.
/// </summary>
public interface IEventService
{
    /// <summary>
    /// Retrieves a list of all events.
    /// </summary>
    /// <returns>A collection of all events.</returns>
    IEnumerable<Event> GetAllEvents();

    /// <summary>
    /// Retrieves an event by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the event.</param>
    /// <returns>The event if found; otherwise, null.</returns>
    Event? GetEventById(Guid id);

    /// <summary>
    /// Creates a new event.
    /// </summary>
    /// <param name="newEvent">The event to create.</param>
    /// <returns>The created event with assigned Id.</returns>
    Event CreateEvent(Event newEvent);

    /// <summary>
    /// Updates an existing event.
    /// </summary>
    /// <param name="id">The unique identifier of the event to update.</param>
    /// <param name="updatedEvent">The updated event data.</param>
    /// <returns>The updated event if found; otherwise, null.</returns>
    Event? UpdateEvent(Guid id, Event updatedEvent);

    /// <summary>
    /// Deletes an event by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the event to delete.</param>
    /// <returns>True if the event was deleted; otherwise, false.</returns>
    bool DeleteEvent(Guid id);
}