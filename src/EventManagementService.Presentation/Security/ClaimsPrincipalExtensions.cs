using System.Security.Claims;
using EventManagementService.Domain.Models;

namespace EventManagementService.Presentation.Security;

/// <summary>
/// Reads the current user's identity and role from JWT claims.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Tries to read the current user's identifier from the <c>NameIdentifier</c> claim.
    /// </summary>
    /// <param name="user">Current principal.</param>
    /// <param name="userId">Parsed user identifier when present and valid.</param>
    /// <returns><c>true</c> if a valid user identifier was found; otherwise <c>false</c>.</returns>
    public static bool TryGetUserId(this ClaimsPrincipal user, out Guid userId)
    {
        userId = Guid.Empty;
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out userId);
    }

    /// <summary>
    /// Reads the current user's role from the <c>Role</c> claim, defaulting to <see cref="UserRole.User"/>.
    /// </summary>
    /// <param name="user">Current principal.</param>
    /// <returns>The parsed role, or <see cref="UserRole.User"/> when the claim is missing or invalid.</returns>
    public static UserRole GetUserRole(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.Role);
        return Enum.TryParse<UserRole>(value, ignoreCase: true, out var role)
            ? role
            : UserRole.User;
    }
}
