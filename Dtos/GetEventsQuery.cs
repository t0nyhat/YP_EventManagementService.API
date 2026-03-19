using System.ComponentModel.DataAnnotations;

namespace EventManagementService.API.Dtos;

/// <summary>
/// Query parameters for retrieving a filtered and paginated list of events.
/// </summary>
public class GetEventsQuery
{
    /// <summary>
    /// Searches events by title using case-insensitive partial match.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Returns events that start no earlier than the specified date.
    /// </summary>
    public DateTime? From { get; set; }

    /// <summary>
    /// Returns events that end no later than the specified date.
    /// </summary>
    public DateTime? To { get; set; }

    /// <summary>
    /// Page number to return. The minimum value is 1.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Номер страницы должен быть не меньше 1.")]
    public int Page { get; set; } = 1;

    /// <summary>
    /// Number of items per page. Allowed range is from 1 to 100.
    /// </summary>
    [Range(1, 100, ErrorMessage = "Размер страницы должен быть в диапазоне от 1 до 100.")]
    public int PageSize { get; set; } = 10;
}
