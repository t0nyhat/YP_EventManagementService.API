using System.Security.Cryptography;
using System.Text;
using EventManagementService.Application.Abstractions.Security;

namespace EventManagementService.Infrastructure.Security;

public sealed class Sha256PasswordHasher : IPasswordHasher
{
    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }

    public bool Verify(string password, string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        byte[] actualHash;

        try
        {
            actualHash = Convert.FromHexString(passwordHash);
        }
        catch (FormatException)
        {
            return false;
        }

        if (actualHash.Length != expectedHash.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}