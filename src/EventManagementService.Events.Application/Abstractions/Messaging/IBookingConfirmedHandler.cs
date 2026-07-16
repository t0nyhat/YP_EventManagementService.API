using EventManagementService.Contracts;

namespace EventManagementService.Events.Application.Abstractions.Messaging;

/// <summary>
/// Handles BookingConfirmed messages from the Bookings service.
/// </summary>
public interface IBookingConfirmedHandler
{
    /// <summary>
    /// Handles a BookingConfirmed message.
    /// Returns true if the message was processed successfully, false if it was skipped.
    /// </summary>
    Task<bool> HandleAsync(BookingConfirmed message, CancellationToken cancellationToken = default);
}