using System.ComponentModel.DataAnnotations;
using EventManagementService.Events.Application.Validation;

namespace EventManagementService.Events.Application.Dtos;

/// <summary>
/// Query parameters for retrieving a filtered and paginated list of events.
/// </summary>
public class GetEventsQuery : IValidatableObject
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
    public int Page { get; set; } = 1;

    /// <summary>
    /// Number of items per page. The minimum value is 1, the maximum is 100.
    /// </summary>
    public int PageSize { get; set; } = 10;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        return GetEventsQueryValidation.Validate(this);
    }
}