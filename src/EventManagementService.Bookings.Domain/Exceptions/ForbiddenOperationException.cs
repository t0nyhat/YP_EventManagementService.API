namespace EventManagementService.Bookings.Domain.Exceptions;

/// <summary>
/// Thrown when the current user is not allowed to access a booking.
/// </summary>
public sealed class ForbiddenOperationException : BusinessValidationException
{
    public ForbiddenOperationException()
        : base("The current user is not allowed to perform this operation.")
    {
    }
}
