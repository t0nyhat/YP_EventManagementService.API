namespace EventManagementService.Events.Domain.Models;

/// <summary>
/// Defines the roles available for users in the system.
/// </summary>
public enum UserRole
{
    /// <summary>
    /// Regular user with basic permissions.
    /// </summary>
    User,

    /// <summary>
    /// Administrator with elevated permissions.
    /// </summary>
    Admin
}