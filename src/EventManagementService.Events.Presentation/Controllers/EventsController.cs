using EventManagementService.Events.Application.Dtos;
using EventManagementService.Events.Application.Abstractions.Services;
using EventManagementService.Events.Application.Mappings;
using EventManagementService.Events.Domain.Models;
using EventManagementService.Events.Presentation.Mappings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventManagementService.Events.Presentation.Controllers;

/// <summary>
/// Controller for managing events.
/// </summary>
[ApiController]
[Route("events")]
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
    public async Task<ActionResult<PaginatedResult<EventResponse>>> GetAllEvents([FromQuery] GetEventsQuery query)
    {
        return Ok((await eventService.GetEventsAsync(query)).ToResponse());
    }

    /// <summary>
    /// Retrieves the top events (up to 10) ranked by the share of sold seats.
    /// </summary>
    /// <remarks>
    /// The result is served from a cache, so it may lag behind the actual data
    /// by up to the configured top-events cache TTL.
    /// </remarks>
    /// <returns>The list of top events.</returns>
    [HttpGet("top")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<EventResponse>>> GetTopEvents()
    {
        return Ok(await eventService.GetTopEventsAsync());
    }

    /// <summary>
    /// Retrieves an event by id.
    /// </summary>
    /// <param name="id">Event identifier.</param>
    /// <returns>Event data if found.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> GetEventById(Guid id)
    {
        // The service already returns the response DTO (it may come straight from the cache).
        return Ok(await eventService.GetEventByIdAsync(id));
    }

    /// <summary>
    /// Creates a new event.
    /// </summary>
    /// <param name="request">Event data to create.</param>
    /// <returns>Created event with server-generated Id.</returns>
    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EventResponse>> CreateEvent([FromBody] CreateEventRequest request)
    {
        var createdEvent = await eventService.CreateEventAsync(request.ToModel());

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
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EventResponse>> UpdateEvent(Guid id, [FromBody] UpdateEventRequest request)
    {
        return Ok((await eventService.UpdateEventAsync(id, request)).ToResponse());
    }

    /// <summary>
    /// Deletes an event.
    /// </summary>
    /// <param name="id">Event identifier.</param>
    /// <returns>No content on success.</returns>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> DeleteEvent(Guid id)
    {
        await eventService.DeleteEventAsync(id);
        return NoContent();
    }
}