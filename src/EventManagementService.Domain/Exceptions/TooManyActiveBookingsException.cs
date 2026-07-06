namespace EventManagementService.Domain.Exceptions;

/// <summary>
/// Thrown when a user exceeds the allowed number of active bookings.
/// </summary>
public sealed class TooManyActiveBookingsException : BusinessValidationException
{
    public TooManyActiveBookingsException(int limit)
        : base($"Превышен лимит активных броней. Максимум: {limit}.")
    {
    }
}
