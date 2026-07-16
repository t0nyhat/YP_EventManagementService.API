using System.Security.Cryptography;
using EventManagementService.Users.Application.Abstractions.Security;

namespace EventManagementService.Users.Infrastructure.Security;

/// <summary>
/// Hashes passwords using PBKDF2 with a 128-bit salt and versioned format.
/// Format: {version}:{salt}:{hash} (all hex-encoded).
/// Version 1 = PBKDF2-HMACSHA256, 600000 iterations, 32-byte output.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;       // 128-bit salt
    private const int HashSize = 32;       // 256-bit hash
    private const int Iterations = 600_000;
    private const string Version = "1";
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            Algorithm,
            HashSize);

        return $"{Version}:{Convert.ToHexString(salt)}:{Convert.ToHexString(hash)}";
    }

    public bool Verify(string password, string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        var segments = passwordHash.Split(':');
        if (segments.Length != 3)
        {
            return false;
        }

        if (segments[0] != Version)
        {
            return false;
        }

        byte[] salt;
        byte[] expectedHash;

        try
        {
            salt = Convert.FromHexString(segments[1]);
            expectedHash = Convert.FromHexString(segments[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length != SaltSize || expectedHash.Length != HashSize)
        {
            return false;
        }

        var computedHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            Algorithm,
            HashSize);

        return CryptographicOperations.FixedTimeEquals(computedHash, expectedHash);
    }
}