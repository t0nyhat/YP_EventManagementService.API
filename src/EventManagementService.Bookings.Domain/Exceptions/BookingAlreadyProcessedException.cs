namespace EventManagementService.Bookings.Domain.Exceptions;

/// <summary>
/// Thrown when an operation is attempted on a booking that cannot be changed anymore.
/// </summary>
public sealed class BookingAlreadyProcessedException : BusinessValidationException
{
    public BookingAlreadyProcessedException(string message)
        : base(message)
    {
    }
}
