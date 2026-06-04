namespace EventManagementService.Domain.Models;

/// <summary>
/// Represents the current processing state of a booking.
/// </summary>
public enum BookingStatus
{
    /// <summary>
    /// The booking has been created and is waiting for background processing.
    /// </summary>
    Pending,

    /// <summary>
    /// The booking has been confirmed.
    /// </summary>
    Confirmed,

    /// <summary>
    /// The booking has been rejected.
    /// </summary>
    Rejected
}