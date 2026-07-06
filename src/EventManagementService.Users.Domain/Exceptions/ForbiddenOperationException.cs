namespace EventManagementService.Users.Domain.Exceptions;

/// <summary>
/// Thrown when a user is not allowed to perform an operation.
/// </summary>
public sealed class ForbiddenOperationException : BusinessValidationException
{
    public ForbiddenOperationException()
        : base("Недостаточно прав для выполнения операции.")
    {
    }
}