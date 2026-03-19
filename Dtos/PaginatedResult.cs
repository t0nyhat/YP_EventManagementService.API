namespace EventManagementService.API.Dtos;

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
    /// Number of items requested per page.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Total number of items after filtering and before pagination.
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// Total number of pages for the filtered result.
    /// </summary>
    public int TotalPages { get; set; }
}
