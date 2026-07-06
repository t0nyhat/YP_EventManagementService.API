using EventManagementService.Bookings.Domain.Models;

namespace EventManagementService.Bookings.Application.Dtos;

/// <summary>
/// Response DTO for booking resources.
/// </summary>
public sealed class BookingResponse
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public BookingStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }
}
