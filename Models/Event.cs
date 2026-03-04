namespace EventManagementService.API.Models;

/// <summary>
/// Represents an event with its details such as title, description, start date, and end date.
/// </summary>
public class Event
{
    /// <summary>
    /// Unique identifier for the event.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Title of the event (required).
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// Detailed description of the event (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Start date and time of the event (required).
    /// </summary>
    public DateTime StartAt { get; set; }

    /// <summary>
    /// End date and time of the event (required). Must be after StartAt.
    /// </summary>
    public DateTime EndAt { get; set; }
}