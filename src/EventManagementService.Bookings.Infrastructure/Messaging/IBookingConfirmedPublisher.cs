namespace EventManagementService.Bookings.Infrastructure.Messaging;

public interface IBookingConfirmedPublisher
{
    Task PublishAsync(Guid eventId, string payload, CancellationToken cancellationToken = default);
}
