using EventManagementService.Bookings.Domain.Exceptions;

namespace EventManagementService.Bookings.Domain.Models;

/// <summary>
/// Represents a local booking record owned by the Bookings service.
/// </summary>
public class Booking
{
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
    /// Unique identifier for the booking.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Identifier of the event this booking was requested for.
    /// </summary>
    public Guid EventId { get; private set; }

    /// <summary>
    /// Identifier of the user who created the booking.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Current local booking status.
    /// </summary>
    public BookingStatus Status { get; private set; }

    /// <summary>
    /// UTC date and time when the booking was created.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// UTC date and time when the booking was last processed.
    /// </summary>
    public DateTime? ProcessedAt { get; private set; }

    /// <summary>
    /// Creates a new pending booking without checking the remote Events service.
    /// </summary>
    public static Booking CreatePending(Guid eventId, Guid userId, DateTime? createdAt = null)
    {
        if (eventId == Guid.Empty)
        {
            throw new BusinessValidationException("Event id must be specified.");
        }

        if (userId == Guid.Empty)
        {
            throw new BusinessValidationException("User id must be specified.");
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
    /// Marks the booking as confirmed.
    /// </summary>
    public void Confirm(DateTime? processedAt = null)
    {
        SetProcessedState(BookingStatus.Confirmed, processedAt ?? DateTime.UtcNow);
    }

    /// <summary>
    /// Marks the booking as rejected.
    /// </summary>
    public void Reject(DateTime? processedAt = null)
    {
        SetProcessedState(BookingStatus.Rejected, processedAt ?? DateTime.UtcNow);
    }

    /// <summary>
    /// Cancels a pending or confirmed booking locally.
    /// </summary>
    public void Cancel(DateTime? processedAt = null)
    {
        if (Status is BookingStatus.Rejected or BookingStatus.Cancelled)
        {
            throw new BookingAlreadyProcessedException("Cancellation is not available for the current booking status.");
        }

        Status = BookingStatus.Cancelled;
        ProcessedAt = processedAt ?? DateTime.UtcNow;
    }

    private void SetProcessedState(BookingStatus targetStatus, DateTime processedAt)
    {
        if (Status != BookingStatus.Pending)
        {
            throw new BookingAlreadyProcessedException("Only pending bookings can be processed.");
        }

        Status = targetStatus;
        ProcessedAt = processedAt;
    }
}
