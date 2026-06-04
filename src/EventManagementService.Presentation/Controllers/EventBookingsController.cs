using EventManagementService.Application.Dtos;
using EventManagementService.Presentation.Mappings;
using EventManagementService.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace EventManagementService.Presentation.Controllers;

/// <summary>
/// Controller for event-related booking operations.
/// </summary>
[ApiController]
[Route("api/events")]
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
    public async Task<ActionResult<BookingResponse>> CreateBooking(Guid id)
    {
        var booking = await bookingService.CreateBookingAsync(id);
        return AcceptedAtRoute(BookingsController.GetBookingByIdRouteName, new { id = booking.Id }, booking.ToResponse());
    }
}
