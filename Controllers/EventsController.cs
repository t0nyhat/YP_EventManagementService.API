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
public class EventsController : ControllerBase
{
    private readonly IEventService _eventService;

    public EventsController(IEventService eventService)
    {
        _eventService = eventService;
    }

    /// <summary>
    /// Retrieves all events.
    /// </summary>
    /// <returns>A list of all events.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetAllEvents()
    {
        var events = _eventService.GetAllEvents();
        var response = events.Select(MapToResponse).ToList();
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
    public IActionResult GetEventById(Guid id)
    {
        var eventItem = _eventService.GetEventById(id);
        if (eventItem is null)
        {
            return NotFound(new { message = $"Событие с id {id} не найдено." });
        }

        return Ok(MapToResponse(eventItem));
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
