using EventManagementService.Application.Dtos;
using EventManagementService.Presentation.Mappings;
using EventManagementService.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EventManagementService.Presentation.Controllers;

/// <summary>
/// Controller for event-related booking operations.
/// </summary>
[ApiController]
[Route("api/events")]
[Authorize]
public class EventBookingsController(IBookingService bookingService) : ControllerBase
{
    /// <summary>
    /// Creates a booking for the specified event.
    /// </summary>
    /// <param name="id">Event identifier.</param>
    /// <returns>Accepted booking resource with a Location header.</returns>
    [HttpPost("{id:guid}/book")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<BookingResponse>> CreateBooking(Guid id)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var booking = await bookingService.CreateBookingAsync(id, currentUserId);
        return AcceptedAtRoute(BookingsController.GetBookingByIdRouteName, new { id = booking.Id }, booking.ToResponse());
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out userId);
    }
}
