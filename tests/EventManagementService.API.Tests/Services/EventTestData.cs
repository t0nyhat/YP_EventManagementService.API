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
        return Event.Create(title, startAt, endAt, totalSeats, description);
    }
}
