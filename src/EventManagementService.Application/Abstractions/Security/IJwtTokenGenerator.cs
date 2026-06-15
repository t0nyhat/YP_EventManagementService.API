using EventManagementService.Domain.Models;

namespace EventManagementService.Application.Abstractions.Security;

/// <summary>
/// Generates JWT tokens for authenticated users.
/// </summary>
public interface IJwtTokenGenerator
{
    /// <summary>
    /// Generates a signed JWT token for the specified user identity.
    /// </summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="login">User login.</param>
    /// <param name="role">User role.</param>
    /// <returns>Signed JWT token string.</returns>
    string GenerateToken(Guid userId, string login, UserRole role);
}