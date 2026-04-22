using EventManagementService.API.Dtos;
using EventManagementService.API.Mappings;
using EventManagementService.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace EventManagementService.API.Controllers;

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
    /// <param name="request">Booking creation request bound from route values.</param>
    /// <returns>Accepted booking resource with a Location header.</returns>
    [HttpPost("{id:guid}/book")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingResponse>> CreateBooking([FromRoute] CreateBookingRequest request)
    {
        var booking = await bookingService.CreateBookingAsync(request.EventId);
        return AcceptedAtRoute(BookingsController.GetBookingByIdRouteName, new { id = booking.Id }, booking.ToResponse());
    }
}
