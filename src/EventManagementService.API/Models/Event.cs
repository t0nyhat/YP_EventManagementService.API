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

    /// <summary>
    /// Total number of seats available for the event.
    /// </summary>
    public int TotalSeats { get; set; }

    /// <summary>
    /// Current number of free seats available for booking.
    /// </summary>
    public int AvailableSeats { get; set; }

    public bool TryReserveSeats(int count = 1)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Количество мест должно быть больше нуля.");
        }

        if (AvailableSeats < count)
        {
            return false;
        }

        AvailableSeats -= count;
        return true;
    }

    public void ReleaseSeats(int count = 1)
    {
        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Количество мест должно быть больше нуля.");
        }

        AvailableSeats = Math.Min(TotalSeats, AvailableSeats + count);
    }
}