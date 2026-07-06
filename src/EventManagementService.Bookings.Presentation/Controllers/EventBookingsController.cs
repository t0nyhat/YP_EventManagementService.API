using EventManagementService.Bookings.Application.Abstractions.Services;
using EventManagementService.Bookings.Application.Dtos;
using EventManagementService.Bookings.Presentation.Mappings;
using EventManagementService.Bookings.Presentation.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventManagementService.Bookings.Presentation.Controllers;

/// <summary>
/// Controller for creating bookings for event identifiers.
/// </summary>
[ApiController]
[Route("events")]
[Authorize]
public sealed class EventBookingsController(IBookingService bookingService) : ControllerBase
{
    [HttpPost("{id:guid}/book")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingResponse>> CreateBooking(Guid id, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var booking = await bookingService.CreateBookingAsync(id, currentUserId, cancellationToken);
        return AcceptedAtRoute(
            BookingsController.GetBookingByIdRouteName,
            new { id = booking.Id },
            booking.ToResponse());
    }
}
