using EventManagementService.Domain.Exceptions;

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
    /// Identifier of the user who created the booking.
    /// </summary>
    public Guid UserId { get; private set; }

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

    private Booking(Guid id, Guid eventId, Guid userId, BookingStatus status, DateTime createdAt, DateTime? processedAt)
    {
        Id = id;
        EventId = eventId;
        UserId = userId;
        Status = status;
        CreatedAt = createdAt;
        ProcessedAt = processedAt;
    }

    /// <summary>
    /// Creates a new booking in Pending state.
    /// </summary>
    /// <param name="eventId">Event identifier the booking is created for.</param>
    /// <param name="userId">User identifier who creates the booking.</param>
    /// <param name="createdAt">Optional explicit creation timestamp for deterministic tests.</param>
    /// <returns>A new booking instance in Pending state.</returns>
    public static Booking CreatePending(Guid eventId, Guid userId, DateTime? createdAt = null)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор события должен быть указан.", nameof(eventId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("Идентификатор пользователя должен быть указан.", nameof(userId));
        }

        return new Booking(
            Guid.NewGuid(),
            eventId,
            userId,
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

    /// <summary>
    /// Cancels the booking and stores the processing timestamp.
    /// Cancellation is allowed for Pending and Confirmed bookings.
    /// </summary>
    /// <param name="processedAt">Optional explicit processing timestamp for deterministic tests.</param>
    public void Cancel(DateTime? processedAt = null)
    {
        if (Status is BookingStatus.Rejected or BookingStatus.Cancelled)
        {
            throw new BookingAlreadyProcessedException(
                "Отмена недоступна для бронирования в текущем статусе.");
        }

        Status = BookingStatus.Cancelled;
        ProcessedAt = processedAt ?? DateTime.UtcNow;
    }

    private void SetProcessedState(BookingStatus targetStatus, DateTime processedAt)
    {
        if (Status != BookingStatus.Pending)
        {
            throw new BookingAlreadyProcessedException(
                "Обрабатывать можно только бронирования в статусе ожидания.");
        }

        Status = targetStatus;
        ProcessedAt = processedAt;
    }
}