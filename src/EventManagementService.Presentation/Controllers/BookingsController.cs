using EventManagementService.Application.Dtos;
using EventManagementService.Application.Services;
using EventManagementService.Domain.Models;
using EventManagementService.Presentation.Mappings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        var booking = await bookingService.GetBookingByIdAsync(id, currentUserId, GetCurrentUserRole());
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
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Unauthorized();
        }

        await bookingService.CancelBookingAsync(id, currentUserId, GetCurrentUserRole());
        return NoContent();
    }

    private UserRole GetCurrentUserRole()
    {
        var value = User.FindFirstValue(ClaimTypes.Role);
        return Enum.TryParse<UserRole>(value, ignoreCase: true, out var role)
            ? role
            : UserRole.User;
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out userId);
    }
}
