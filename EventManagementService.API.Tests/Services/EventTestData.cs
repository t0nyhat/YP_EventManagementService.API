using EventManagementService.API.Models;

namespace EventManagementService.API.Tests.Services;

internal static class EventTestData
{
    public static Event CreateEvent(
        string title,
        string? description,
        DateTime startAt,
        DateTime endAt,
        int totalSeats = 10)
    {
        return new Event
        {
            Title = title,
            Description = description,
            StartAt = startAt,
            EndAt = endAt,
            TotalSeats = totalSeats,
            AvailableSeats = totalSeats
        };
    }
}
