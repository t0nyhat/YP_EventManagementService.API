using EventManagementService.API.Dtos;
using EventManagementService.API.Models;

namespace EventManagementService.API.Mappings;

public static class EventMappings
{
    public static Event ToModel(this CreateEventRequest request)
    {
        return new Event
        {
            Title = request.Title,
            Description = request.Description,
            StartAt = request.StartAt!.Value,
            EndAt = request.EndAt!.Value,
            TotalSeats = request.TotalSeats!.Value,
            AvailableSeats = request.TotalSeats.Value
        };
    }

    public static Event ToModel(this UpdateEventRequest request)
    {
        return new Event
        {
            Title = request.Title,
            Description = request.Description,
            StartAt = request.StartAt!.Value,
            EndAt = request.EndAt!.Value
        };
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
