namespace EventManagementService.Domain.Exceptions;

/// <summary>
/// Thrown when an operation is attempted on a booking that has already been processed.
/// </summary>
public sealed class BookingAlreadyProcessedException : BusinessValidationException
{
    public BookingAlreadyProcessedException(string message)
        : base(message)
    {
    }
}