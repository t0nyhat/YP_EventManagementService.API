using EventManagementService.Events.Application.Dtos;
using EventManagementService.Events.Domain.Models;

namespace EventManagementService.Events.Application.Mappings;

/// <summary>
/// Single source of truth for mapping domain events to response DTOs.
/// Lives in Application because <see cref="EventResponse"/> is the payload
/// stored in the cache, so the application service must be able to build it;
/// Presentation reuses these extensions instead of duplicating the mapping.
/// </summary>
public static class EventMappings
{
    /// <summary>
    /// Maps a domain <see cref="Event"/> to its response DTO.
    /// </summary>
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

    /// <summary>
    /// Maps a page of domain events to a page of response DTOs.
    /// </summary>
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
