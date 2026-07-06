namespace EventManagementService.Bookings.Domain.Models;

/// <summary>
/// User roles understood by the Bookings service from JWT claims.
/// </summary>
public enum UserRole
{
    User,
    Admin
}
