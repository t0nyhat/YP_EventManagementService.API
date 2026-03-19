using EventManagementService.API.Dtos;
using EventManagementService.API.Models;
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
        var events = eventService.GetEvents(query);
        var items = events.Items.Select(MapToResponse).ToArray();

        var response = new PaginatedResult<EventResponse>
        {
            Items = items,
            Page = events.Page,
            PageSize = events.PageSize,
            TotalItems = events.TotalItems,
            TotalPages = events.TotalPages
        };

        return Ok(response);
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
        var eventItem = eventService.GetEventById(id);
        return Ok(MapToResponse(eventItem));
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
        // Map request to domain model.
        // Safe to use .Value since [Required] validation already passed.
        var eventItem = new Event
        {
            Title = request.Title,
            Description = request.Description,
            StartAt = request.StartAt!.Value,
            EndAt = request.EndAt!.Value
        };

        // Create event (service generates Id).
        var createdEvent = eventService.CreateEvent(eventItem);

        // Return 201 Created with Location header pointing to the created resource.
        var response = MapToResponse(createdEvent);
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
        // Map request to domain model.
        var eventItem = new Event
        {
            Title = request.Title,
            Description = request.Description,
            StartAt = request.StartAt!.Value,
            EndAt = request.EndAt!.Value
        };

        // Update event.
        var updatedEvent = eventService.UpdateEvent(id, eventItem);
        return Ok(MapToResponse(updatedEvent));
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

    private static EventResponse MapToResponse(Event eventItem)
    {
        return new EventResponse
        {
            Id = eventItem.Id,
            Title = eventItem.Title,
            Description = eventItem.Description,
            StartAt = eventItem.StartAt,
            EndAt = eventItem.EndAt
        };
    }
}
