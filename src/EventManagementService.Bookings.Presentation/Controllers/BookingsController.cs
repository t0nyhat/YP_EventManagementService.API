using EventManagementService.Bookings.Application.Abstractions.Services;
using EventManagementService.Bookings.Application.Dtos;
using EventManagementService.Bookings.Presentation.Mappings;
using EventManagementService.Bookings.Presentation.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventManagementService.Bookings.Presentation.Controllers;

/// <summary>
/// Controller for booking resource operations.
/// </summary>
[ApiController]
[Route("bookings")]
[Authorize]
public sealed class BookingsController(IBookingService bookingService) : ControllerBase
{
    public const string GetBookingByIdRouteName = "GetBookingById";

    [HttpGet("{id:guid}", Name = GetBookingByIdRouteName)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingResponse>> GetBookingById(Guid id, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var booking = await bookingService.GetBookingByIdAsync(
            id,
            currentUserId,
            User.GetUserRole(),
            cancellationToken);

        return Ok(booking.ToResponse());
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> CancelBooking(Guid id, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        await bookingService.CancelBookingAsync(
            id,
            currentUserId,
            User.GetUserRole(),
            cancellationToken);

        return NoContent();
    }
}
