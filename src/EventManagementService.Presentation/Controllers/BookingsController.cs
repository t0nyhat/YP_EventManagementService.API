using EventManagementService.Application.Dtos;
using EventManagementService.Application.Services;
using EventManagementService.Presentation.Mappings;
using EventManagementService.Presentation.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventManagementService.Presentation.Controllers;

/// <summary>
/// Controller for reading booking resources.
/// </summary>
[ApiController]
[Route("api/bookings")]
[Authorize]
public class BookingsController(IBookingService bookingService) : ControllerBase
{
    public const string GetBookingByIdRouteName = "GetBookingById";

    /// <summary>
    /// Retrieves a booking by id.
    /// </summary>
    /// <param name="id">Booking identifier.</param>
    /// <returns>Booking data if found.</returns>
    [HttpGet("{id:guid}", Name = GetBookingByIdRouteName)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BookingResponse>> GetBookingById(Guid id)
    {
        if (!User.TryGetUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var booking = await bookingService.GetBookingByIdAsync(id, currentUserId, User.GetUserRole());
        return Ok(booking.ToResponse());
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> CancelBooking(Guid id)
    {
        if (!User.TryGetUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        await bookingService.CancelBookingAsync(id, currentUserId, User.GetUserRole());
        return NoContent();
    }
}
