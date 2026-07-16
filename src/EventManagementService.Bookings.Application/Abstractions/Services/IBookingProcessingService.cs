namespace EventManagementService.Bookings.Application.Abstractions.Services;

public interface IBookingProcessingService
{
    Task ProcessPendingBookingAsync(Guid bookingId, CancellationToken cancellationToken = default);
}
