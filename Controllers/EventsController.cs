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
