using System.ComponentModel.DataAnnotations;

namespace EventManagementService.Application.Dtos;

/// <summary>
/// Request data transfer object for updating an existing event.
/// Id is specified in the URL path, not in the request body.
/// </summary>
public class UpdateEventRequest
{
    /// <summary>
    /// Title of the event (required).
    /// </summary>
    [Required(ErrorMessage = "Название события обязательно")]
    public required string Title { get; set; }

    /// <summary>
    /// Detailed description of the event (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Start date and time of the event (required).
    /// </summary>
    [Required(ErrorMessage = "Дата начала события обязательна")]
    public DateTime? StartAt { get; set; }

    /// <summary>
    /// End date and time of the event (required).
    /// Must be after StartAt.
    /// </summary>
    [Required(ErrorMessage = "Дата окончания события обязательна")]
    [Compare("StartAt", ErrorMessage = "Дата окончания должна быть позже даты начала.")]
    public DateTime? EndAt { get; set; }
}