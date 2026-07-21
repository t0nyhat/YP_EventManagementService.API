using EventManagementService.Events.Application.Dtos;
using EventManagementService.Events.Domain.Models;

namespace EventManagementService.Events.Presentation.Mappings;

/// <summary>
/// Presentation-side input mapping only. Output mapping (Event -&gt; EventResponse)
/// lives in Application (<c>Application.Mappings.EventMappings</c>) because the
/// application service caches the response DTO; reuse it instead of duplicating.
/// </summary>
public static class EventMappings
{
    public static Event ToModel(this CreateEventRequest request)
    {
        return Event.Create(
            request.Title,
            request.StartAt!.Value,
            request.EndAt!.Value,
            request.TotalSeats!.Value,
            request.Description);
    }
}
