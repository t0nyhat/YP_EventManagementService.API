namespace EventManagementService.Contracts;

public static class KafkaTopics
{
    public const string BookingConfirmed = "booking-confirmed";

    /// <summary>
    /// Dead Letter Topic for BookingConfirmed messages the Events consumer could not
    /// process (permanently invalid payload, or a transient failure that persisted
    /// past the retry limit). Isolates the message so the main topic keeps flowing.
    /// </summary>
    public const string BookingConfirmedDeadLetter = "booking-confirmed.DLT";
}

public sealed record BookingConfirmed(
    Guid BookingId,
    Guid EventId,
    Guid UserId,
    int Seats,
    DateTimeOffset ConfirmedAtUtc);