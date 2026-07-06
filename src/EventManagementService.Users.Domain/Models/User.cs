using EventManagementService.Users.Domain.Exceptions;

namespace EventManagementService.Users.Domain.Models;

/// <summary>
/// Represents an application user with credentials and role.
/// </summary>
public class User
{
    private User()
    {
        Login = null!;
        PasswordHash = null!;
    }

    private User(Guid id, string login, string passwordHash, UserRole role)
    {
        Id = id;
        Login = login;
        PasswordHash = passwordHash;
        Role = role;
    }

    /// <summary>
    /// Unique identifier for the user.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// User login.
    /// </summary>
    public string Login { get; private set; }

    /// <summary>
    /// Password hash.
    /// </summary>
    public string PasswordHash { get; private set; }

    /// <summary>
    /// User role.
    /// </summary>
    public UserRole Role { get; private set; }

    public static User Create(string login, string passwordHash, UserRole role = UserRole.User, Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(login))
        {
            throw new BusinessValidationException("Логин пользователя не должен быть пустым.");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new BusinessValidationException("Хеш пароля пользователя не должен быть пустым.");
        }

        return new User(id ?? Guid.NewGuid(), login.Trim(), passwordHash, role);
    }
}