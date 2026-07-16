using System.ComponentModel.DataAnnotations;

namespace EventManagementService.Events.Application.Dtos;

/// <summary>
/// Response data transfer object for event queries.
/// Includes all event details including server-generated Id.
/// </summary>
public class EventResponse
{
    /// <summary>
    /// Unique identifier for the event (generated server-side).
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Title of the event.
    /// </summary>
    [Required]
    public required string Title { get; set; }

    /// <summary>
    /// Detailed description of the event.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Start date and time of the event.
    /// </summary>
    [Required]
    public DateTime StartAt { get; set; }

    /// <summary>
    /// End date and time of the event.
    /// </summary>
    [Required]
    public DateTime EndAt { get; set; }

    /// <summary>
    /// Total number of seats available for the event.
    /// </summary>
    [Required]
    public int TotalSeats { get; set; }

    /// <summary>
    /// Number of seats still available for booking.
    /// </summary>
    [Required]
    public int AvailableSeats { get; set; }
}