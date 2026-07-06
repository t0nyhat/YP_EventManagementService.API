namespace EventManagementService.Application.Abstractions.Security;

/// <summary>
/// Provides password hashing and verification operations.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Hashes a plain text password.
    /// </summary>
    /// <param name="password">Plain text password.</param>
    /// <returns>Hashed password value.</returns>
    string Hash(string password);

    /// <summary>
    /// Verifies that a plain text password matches the stored hash.
    /// </summary>
    /// <param name="password">Plain text password.</param>
    /// <param name="passwordHash">Stored hash value.</param>
    /// <returns><c>true</c> if the password matches; otherwise <c>false</c>.</returns>
    bool Verify(string password, string passwordHash);
}