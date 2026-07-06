using System.Text.Json.Serialization;

namespace EventManagementService.Bookings.Domain.Models;

/// <summary>
/// Represents the current lifecycle state of a booking.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<BookingStatus>))]
public enum BookingStatus
{
    Pending,
    Confirmed,
    Rejected,
    Cancelled
}
