using Microsoft.AspNetCore.Mvc;

namespace EventManagementService.API.Dtos;

/// <summary>
/// Request data transfer object for creating a booking for a specific event.
/// The event identifier is expected to come from the route.
/// </summary>
public class CreateBookingRequest
{
    /// <summary>
    /// Identifier of the event to book.
    /// </summary>
    [FromRoute(Name = "id")]
    public Guid EventId { get; set; }
}
