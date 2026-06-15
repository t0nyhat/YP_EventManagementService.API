namespace EventManagementService.Application.Services;

/// <summary>
/// Service contract for background processing of pending bookings.
/// Encapsulates the business decisions for confirming or rejecting a pending booking.
/// </summary>
public interface IBookingProcessingService
{
    /// <summary>
    /// Processes a single pending booking:
    /// - skips if the booking is no longer pending;
    /// - rejects if the associated event has been deleted;
    /// - confirms on successful processing;
    /// - on error, rejects the booking and releases the event seat if the event exists.
    /// </summary>
    /// <param name="bookingId">The identifier of the booking to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ProcessPendingBookingAsync(Guid bookingId, CancellationToken cancellationToken = default);
}