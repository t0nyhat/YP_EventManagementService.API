using EventManagementService.Application.Dtos;
using EventManagementService.Application.Services;
using EventManagementService.Presentation.Mappings;
using Microsoft.AspNetCore.Mvc;

namespace EventManagementService.Presentation.Controllers;

/// <summary>
/// Controller for reading booking resources.
/// </summary>
[ApiController]
[Route("api/bookings")]
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
    public async Task<ActionResult<BookingResponse>> GetBookingById(Guid id)
    {
        var booking = await bookingService.GetBookingByIdAsync(id);
        return Ok(booking.ToResponse());
    }
}
