using EventManagementService.API.Dtos;
using EventManagementService.API.Exceptions;
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
    /// Retrieves a filtered and paginated list of events.
    /// </summary>
    /// <param name="query">Filtering and pagination parameters.</param>
    /// <returns>A paginated result of events.</returns>
    /// <exception cref="BusinessValidationException">Thrown when pagination parameters are invalid.</exception>
    PaginatedResult<Event> GetEvents(GetEventsQuery query);

    /// <summary>
    /// Retrieves an event by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the event.</param>
    /// <returns>The event if found.</returns>
    /// <exception cref="NotFoundException">Thrown when the event does not exist.</exception>
    Event GetEventById(Guid id);

    /// <summary>
    /// Creates a new event.
    /// </summary>
    /// <param name="newEvent">The event to create.</param>
    /// <returns>The created event with assigned Id.</returns>
    /// <exception cref="BusinessValidationException">Thrown when event data is invalid.</exception>
    Event CreateEvent(Event newEvent);

    /// <summary>
    /// Updates an existing event.
    /// </summary>
    /// <param name="id">The unique identifier of the event to update.</param>
    /// <param name="updatedEvent">The updated event data.</param>
    /// <returns>The updated event.</returns>
    /// <exception cref="BusinessValidationException">Thrown when event data is invalid.</exception>
    /// <exception cref="NotFoundException">Thrown when the event does not exist.</exception>
    Event UpdateEvent(Guid id, Event updatedEvent);

    /// <summary>
    /// Deletes an event by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the event to delete.</param>
    /// <exception cref="NotFoundException">Thrown when the event does not exist.</exception>
    void DeleteEvent(Guid id);
}
