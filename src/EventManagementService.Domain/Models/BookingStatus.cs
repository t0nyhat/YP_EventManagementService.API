using System.Text.Json.Serialization;

namespace EventManagementService.Domain.Models;

/// <summary>
/// Represents the current processing state of a booking.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<BookingStatus>))]
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
    Rejected,

    /// <summary>
    /// The booking has been cancelled.
    /// </summary>
    Cancelled
}