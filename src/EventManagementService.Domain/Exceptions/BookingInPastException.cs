namespace EventManagementService.Domain.Exceptions;

/// <summary>
/// Thrown when attempting to book an event that has already started.
/// </summary>
public sealed class BookingInPastException : BusinessValidationException
{
    public BookingInPastException()
        : base("Нельзя бронировать событие, которое уже началось.")
    {
    }
}
