using EventManagementService.API.Dtos;
using EventManagementService.Domain.Models;

namespace EventManagementService.API.Mappings;

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

    public static EventResponse ToResponse(this Event eventItem)
    {
        return new EventResponse
        {
            Id = eventItem.Id,
            Title = eventItem.Title,
            Description = eventItem.Description,
            StartAt = eventItem.StartAt,
            EndAt = eventItem.EndAt,
            TotalSeats = eventItem.TotalSeats,
            AvailableSeats = eventItem.AvailableSeats
        };
    }

    public static PaginatedResult<EventResponse> ToResponse(this PaginatedResult<Event> events)
    {
        return new PaginatedResult<EventResponse>
        {
            Items = events.Items.Select(ToResponse).ToArray(),
            Page = events.Page,
            Count = events.Count,
            TotalCount = events.TotalCount
        };
    }
}
