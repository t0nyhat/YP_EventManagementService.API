using EventManagementService.Events.Domain.Exceptions;

namespace EventManagementService.Events.Domain.Models;

/// <summary>
/// Represents an event with its details such as title, description, start date, and end date.
/// </summary>
public class Event
{
    // Требуется EF Core для материализации сущностей из БД через рефлексию.
    private Event() { Title = null!; }

    private Event(Guid id, string title, DateTime startAt, DateTime endAt, int totalSeats, string? description)
    {
        Id = id;
        Title = title;
        StartAt = startAt;
        EndAt = endAt;
        TotalSeats = totalSeats;
        AvailableSeats = totalSeats;
        Description = description;
    }

    /// <summary>
    /// Unique identifier for the event.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Title of the event.
    /// </summary>
    public string Title { get; private set; }

    /// <summary>
    /// Detailed description of the event (optional).
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Start date and time of the event.
    /// </summary>
    public DateTime StartAt { get; private set; }

    /// <summary>
    /// End date and time of the event. Must be after StartAt.
    /// </summary>
    public DateTime EndAt { get; private set; }

    /// <summary>
    /// Total number of seats available for the event.
    /// </summary>
    public int TotalSeats { get; private set; }

    /// <summary>
    /// Current number of free seats available for booking.
    /// </summary>
    public int AvailableSeats { get; private set; }

    /// <summary>
    /// Creates a new event with a server-generated Id.
    /// </summary>
    public static Event Create(string title, DateTime startAt, DateTime endAt, int totalSeats, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new BusinessValidationException("Название события не должно быть пустым.");

        if (endAt <= startAt)
            throw new BusinessValidationException("Дата окончания должна быть позже даты начала события.");

        if (totalSeats <= 0)
            throw new BusinessValidationException("Количество мест должно быть больше нуля.");

        return new Event(Guid.NewGuid(), title.Trim(), startAt, endAt, totalSeats, description);
    }

    /// <summary>
    /// Updates mutable fields of an existing event.
    /// </summary>
    public void Update(string title, DateTime startAt, DateTime endAt, string? description)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new BusinessValidationException("Название события не должно быть пустым.");

        if (endAt <= startAt)
            throw new BusinessValidationException("Дата окончания должна быть позже даты начала события.");

        Title = title.Trim();
        StartAt = startAt;
        EndAt = endAt;
        Description = description;
    }

    /// <summary>
    /// Tries to decrease available seats by the specified count.
    /// Returns false if there are not enough seats.
    /// </summary>
    public bool TryDecreaseAvailableSeats(int count = 1)
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

}
