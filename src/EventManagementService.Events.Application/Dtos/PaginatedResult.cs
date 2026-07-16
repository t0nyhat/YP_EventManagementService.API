namespace EventManagementService.Events.Application.Dtos;

/// <summary>
/// Represents a paginated response.
/// </summary>
/// <typeparam name="T">Type of items in the paginated result.</typeparam>
public class PaginatedResult<T>
{
    /// <summary>
    /// Items returned for the current page.
    /// </summary>
    public IReadOnlyCollection<T> Items { get; set; } = Array.Empty<T>();

    /// <summary>
    /// Current page number.
    /// </summary>
    public int Page { get; set; }

    /// <summary>
    /// Number of items returned for the current page.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Total number of items after filtering and before pagination.
    /// </summary>
    public int TotalCount { get; set; }
}