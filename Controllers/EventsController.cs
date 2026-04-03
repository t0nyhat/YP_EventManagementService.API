using EventManagementService.API.Dtos;
using EventManagementService.API.Mappings;
using EventManagementService.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace EventManagementService.API.Controllers;

/// <summary>
/// Controller for managing events.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EventsController(IEventService eventService) : ControllerBase
{
    /// <summary>
    /// Retrieves a filtered and paginated list of events.
    /// </summary>
    /// <param name="query">Filtering and pagination parameters.</param>
    /// <returns>A paginated list of events.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<PaginatedResult<EventResponse>> GetAllEvents([FromQuery] GetEventsQuery query)
    {
        return Ok(eventService.GetEvents(query).ToResponse());
    }

    /// <summary>
    /// Retrieves an event by id.
    /// </summary>
    /// <param name="id">Event identifier.</param>
    /// <returns>Event data if found.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<EventResponse> GetEventById(Guid id)
    {
        return Ok(eventService.GetEventById(id).ToResponse());
    }

    /// <summary>
    /// Creates a new event.
    /// </summary>
    /// <param name="request">Event data to create.</param>
    /// <returns>Created event with server-generated Id.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<EventResponse> CreateEvent([FromBody] CreateEventRequest request)
    {
        // Create event (service generates Id).
        var createdEvent = eventService.CreateEvent(request.ToModel());

        // Return 201 Created with Location header pointing to the created resource.
        var response = createdEvent.ToResponse();
        return CreatedAtAction(nameof(GetEventById), new { id = createdEvent.Id }, response);
    }

    /// <summary>
    /// Updates an existing event.
    /// </summary>
    /// <param name="id">Event identifier.</param>
    /// <param name="request">Updated event data.</param>
    /// <returns>Updated event.</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<EventResponse> UpdateEvent(Guid id, [FromBody] UpdateEventRequest request)
    {
        // Update event.
        return Ok(eventService.UpdateEvent(id, request.ToModel()).ToResponse());
    }

    /// <summary>
    /// Deletes an event.
    /// </summary>
    /// <param name="id">Event identifier.</param>
    /// <returns>No content on success.</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult DeleteEvent(Guid id)
    {
        eventService.DeleteEvent(id);
        return NoContent();
    }
}
