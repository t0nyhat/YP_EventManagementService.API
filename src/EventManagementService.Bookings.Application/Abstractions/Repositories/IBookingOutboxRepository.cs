using EventManagementService.Contracts;

namespace EventManagementService.Bookings.Application.Abstractions.Repositories;

public interface IBookingOutboxRepository
{
    Task AddAsync(
        BookingConfirmed message,
        string payload,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default);
}
