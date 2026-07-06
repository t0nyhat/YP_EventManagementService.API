namespace EventManagementService.Bookings.Domain.Exceptions;

/// <summary>
/// Base exception for domain and application validation errors.
/// </summary>
public class BusinessValidationException : Exception
{
    public BusinessValidationException(string message)
        : base(message)
    {
    }
}
