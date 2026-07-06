using System.Security.Claims;
using EventManagementService.Bookings.Domain.Models;

namespace EventManagementService.Bookings.Presentation.Security;

/// <summary>
/// Reads current-user identity data from JWT claims.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    public static bool TryGetUserId(this ClaimsPrincipal user, out Guid userId)
    {
        userId = Guid.Empty;
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out userId);
    }

    public static UserRole GetUserRole(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.Role);
        return Enum.TryParse<UserRole>(value, ignoreCase: true, out var role)
            ? role
            : UserRole.User;
    }
}
