namespace EventManagementService.Domain.Models;

/// <summary>
/// Represents a booking created for a specific event.
/// </summary>
public class Booking
{
    /// <summary>
    /// Unique identifier for the booking.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Identifier of the event this booking belongs to.
    /// </summary>
    public Guid EventId { get; private set; }

    /// <summary>
    /// Current booking processing status.
    /// </summary>
    public BookingStatus Status { get; private set; }

    /// <summary>
    /// Date and time when the booking was created.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Date and time when the booking was processed.
    /// </summary>
    public DateTime? ProcessedAt { get; private set; }

    public Event? Event { get; private set; }

    private Booking()
    {
    }

    private Booking(Guid id, Guid eventId, BookingStatus status, DateTime createdAt, DateTime? processedAt)
    {
        Id = id;
        EventId = eventId;
        Status = status;
        CreatedAt = createdAt;
        ProcessedAt = processedAt;
    }

    /// <summary>
    /// Creates a new booking in Pending state.
    /// </summary>
    /// <param name="eventId">Event identifier the booking is created for.</param>
    /// <param name="createdAt">Optional explicit creation timestamp for deterministic tests.</param>
    /// <returns>A new booking instance in Pending state.</returns>
    public static Booking CreatePending(Guid eventId, DateTime? createdAt = null)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор события должен быть указан.", nameof(eventId));
        }

        return new Booking(
            Guid.NewGuid(),
            eventId,
            BookingStatus.Pending,
            createdAt ?? DateTime.UtcNow,
            processedAt: null);
    }

    /// <summary>
    /// Marks the booking as confirmed and stores processing timestamp.
    /// </summary>
    /// <param name="processedAt">Optional explicit processing timestamp for deterministic tests.</param>
    public void Confirm(DateTime? processedAt = null)
    {
        SetProcessedState(BookingStatus.Confirmed, processedAt ?? DateTime.UtcNow);
    }

    /// <summary>
    /// Marks the booking as rejected and stores processing timestamp.
    /// </summary>
    /// <param name="processedAt">Optional explicit processing timestamp for deterministic tests.</param>
    public void Reject(DateTime? processedAt = null)
    {
        SetProcessedState(BookingStatus.Rejected, processedAt ?? DateTime.UtcNow);
    }

    private void SetProcessedState(BookingStatus targetStatus, DateTime processedAt)
    {
        if (Status != BookingStatus.Pending)
        {
            throw new InvalidOperationException("Обрабатывать можно только бронирования в статусе ожидания.");
        }

        Status = targetStatus;
        ProcessedAt = processedAt;
    }
}