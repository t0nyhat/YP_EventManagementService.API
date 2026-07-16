namespace EventManagementService.Bookings.Domain.Exceptions;

/// <summary>
/// Thrown when a user exceeds the configured active-booking limit.
/// </summary>
public sealed class TooManyActiveBookingsException : BusinessValidationException
{
    public TooManyActiveBookingsException(int limit)
        : base($"Превышен лимит активных бронирований. Максимально допустимо: {limit}.")
    {
    }
}
