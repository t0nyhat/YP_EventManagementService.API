using EventManagementService.Contracts;

namespace EventManagementService.Bookings.Infrastructure.DataAccess;

/// <summary>
/// Durable outbox row for publishing BookingConfirmed messages.
/// </summary>
public class BookingOutboxMessage
{
    private const int MaxLastErrorLength = 2000;

    private BookingOutboxMessage()
    {
        Payload = null!;
    }

    private BookingOutboxMessage(
        Guid id,
        Guid bookingId,
        Guid eventId,
        Guid userId,
        int seats,
        DateTimeOffset confirmedAtUtc,
        string payload,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        BookingId = bookingId;
        EventId = eventId;
        UserId = userId;
        Seats = seats;
        ConfirmedAtUtc = confirmedAtUtc;
        Payload = payload;
        CreatedAtUtc = createdAtUtc;
        PublishAttempts = 0;
    }

    public Guid Id { get; private set; }

    public Guid BookingId { get; private set; }

    public Guid EventId { get; private set; }

    public Guid UserId { get; private set; }

    public int Seats { get; private set; }

    public DateTimeOffset ConfirmedAtUtc { get; private set; }

    public string Payload { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? PublishedAtUtc { get; private set; }

    public int PublishAttempts { get; private set; }

    public string? LastError { get; private set; }

    public static BookingOutboxMessage Create(
        BookingConfirmed message,
        string payload,
        DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        return new BookingOutboxMessage(
            Guid.NewGuid(),
            message.BookingId,
            message.EventId,
            message.UserId,
            message.Seats,
            message.ConfirmedAtUtc,
            payload,
            createdAtUtc);
    }

    public void MarkPublished(DateTimeOffset publishedAtUtc)
    {
        PublishedAtUtc = publishedAtUtc;
        LastError = null;
    }

    public void RecordFailure(string error)
    {
        PublishAttempts++;
        LastError = error.Length <= MaxLastErrorLength
            ? error
            : error[..MaxLastErrorLength];
    }
}
