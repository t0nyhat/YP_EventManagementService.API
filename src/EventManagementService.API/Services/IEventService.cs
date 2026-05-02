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

    /// <summary>
    /// Atomically attempts to reserve one seat for the specified event.
    /// </summary>
    /// <param name="eventId">The unique identifier of the event.</param>
    /// <returns><c>true</c> if a seat was reserved; <c>false</c> if no seats are available.</returns>
    /// <exception cref="NotFoundException">Thrown when the event does not exist.</exception>
    bool TryReserveSeats(Guid eventId);

    /// <summary>
    /// Atomically releases one seat back to the specified event.
    /// Has no effect if the event no longer exists.
    /// </summary>
    /// <param name="eventId">The unique identifier of the event.</param>
    void ReleaseSeats(Guid eventId);
}
