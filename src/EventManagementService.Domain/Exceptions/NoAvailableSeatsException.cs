namespace EventManagementService.Domain.Exceptions;

/// <summary>
/// Thrown when a booking cannot be created because no seats are available for the event.
/// </summary>
public class NoAvailableSeatsException : Exception
{
    public NoAvailableSeatsException(string message) : base(message) { }
}