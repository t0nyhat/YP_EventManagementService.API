namespace EventManagementService.Events.Infrastructure.DataAccess;

/// <summary>
/// Represents a processed BookingConfirmed message for idempotency.
/// </summary>
public class BookingConfirmedInbox
{
    public Guid BookingId { get; set; }
    public Guid EventId { get; set; }
    public Guid UserId { get; set; }
    public int Seats { get; set; }
    public DateTimeOffset ConfirmedAtUtc { get; set; }
    public DateTimeOffset ProcessedAtUtc { get; set; }
    public string Result { get; set; } = null!;
}