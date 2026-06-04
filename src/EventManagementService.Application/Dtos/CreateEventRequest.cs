using System.ComponentModel.DataAnnotations;

namespace EventManagementService.Application.Dtos;

/// <summary>
/// Request data transfer object for creating a new event.
/// Id is generated server-side and not accepted from client.
/// </summary>
public class CreateEventRequest
{
    /// <summary>
    /// Title of the event (required).
    /// </summary>
    [Required(ErrorMessage = "Название события обязательно!")]
    public required string Title { get; set; }

    /// <summary>
    /// Detailed description of the event (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Start date and time of the event (required).
    /// </summary>
    [Required(ErrorMessage = "Дата начала события обязательна!")]
    public DateTime? StartAt { get; set; }

    /// <summary>
    /// End date and time of the event (required).
    /// Must be after StartAt.
    /// </summary>
    [Required(ErrorMessage = "Дата окончания события обязательна!")]
    public DateTime? EndAt { get; set; }

    /// <summary>
    /// Total number of seats available for the event (required).
    /// </summary>
    [Required(ErrorMessage = "Количество мест обязательно!")]
    [Range(1, int.MaxValue, ErrorMessage = "Количество мест должно быть больше нуля.")]
    public int? TotalSeats { get; set; }
}
