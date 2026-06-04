using EventManagementService.Domain.Models;

namespace EventManagementService.API.Dtos;

/// <summary>
/// Response data transfer object for booking queries.
/// </summary>
public class BookingResponse
{
    /// <summary>
    /// Unique identifier for the booking.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Identifier of the booked event.
    /// </summary>
    public Guid EventId { get; set; }

    /// <summary>
    /// Current booking processing status.
    /// </summary>
    public BookingStatus Status { get; set; }

    /// <summary>
    /// Date and time when the booking was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Date and time when the booking was processed.
    /// </summary>
    public DateTime? ProcessedAt { get; set; }
}
