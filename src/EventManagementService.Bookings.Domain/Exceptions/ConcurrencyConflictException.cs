namespace EventManagementService.Bookings.Domain.Exceptions;

/// <summary>
/// The booking was modified by a concurrent operation (e.g. cancelled while being confirmed).
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
