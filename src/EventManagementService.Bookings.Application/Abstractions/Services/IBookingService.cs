using EventManagementService.Bookings.Domain.Models;

namespace EventManagementService.Bookings.Application.Abstractions.Services;

public interface IBookingService
{
    Task<Booking> CreateBookingAsync(Guid eventId, Guid userId, CancellationToken cancellationToken = default);

    Task<Booking> GetBookingByIdAsync(
        Guid bookingId,
        Guid requesterUserId,
        UserRole requesterRole,
        CancellationToken cancellationToken = default);

    Task CancelBookingAsync(
        Guid bookingId,
        Guid requesterUserId,
        UserRole requesterRole,
        CancellationToken cancellationToken = default);
}
