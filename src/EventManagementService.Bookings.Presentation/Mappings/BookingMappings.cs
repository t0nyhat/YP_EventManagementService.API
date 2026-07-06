using EventManagementService.Bookings.Application.Dtos;
using EventManagementService.Bookings.Domain.Models;

namespace EventManagementService.Bookings.Presentation.Mappings;

public static class BookingMappings
{
    public static BookingResponse ToResponse(this Booking booking)
    {
        return new BookingResponse
        {
            Id = booking.Id,
            EventId = booking.EventId,
            Status = booking.Status,
            CreatedAt = booking.CreatedAt,
            ProcessedAt = booking.ProcessedAt
        };
    }
}
