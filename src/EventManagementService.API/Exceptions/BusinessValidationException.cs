namespace EventManagementService.API.Exceptions;

/// <summary>
/// Thrown when a business rule validation fails.
/// </summary>
public sealed class BusinessValidationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessValidationException"/> class.
    /// </summary>
    public BusinessValidationException()
        : base("Ошибка бизнес-валидации.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessValidationException"/> class
    /// with a specified error message.
    /// </summary>
    /// <param name="message">Message that describes the validation error.</param>
    public BusinessValidationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessValidationException"/> class
    /// with a specified error message and a reference to the inner exception.
    /// </summary>
    /// <param name="message">Message that describes the validation error.</param>
    /// <param name="innerException">The exception that caused the current exception.</param>
    public BusinessValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
