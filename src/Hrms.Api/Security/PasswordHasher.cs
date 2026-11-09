using System.Globalization;
using System.Security.Cryptography;

namespace Hrms.Api.Security;

public static class PasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] derivedKey = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

        try
        {
            return string.Concat(
                "PBKDF2$",
                Iterations.ToString(CultureInfo.InvariantCulture),
                "$",
                Convert.ToBase64String(salt),
                "$",
                Convert.ToBase64String(derivedKey));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derivedKey);
        }
    }

    public static bool Verify(string password, string hash)
    {
        if (password is null || hash is null || hash.Length > 128)
        {
            return false;
        }

        string[] parts = hash.Split('$');

        if (parts.Length != 4 ||
            !string.Equals(parts[0], "PBKDF2", StringComparison.Ordinal) ||
            !string.Equals(parts[1], "100000", StringComparison.Ordinal))
        {
            return false;
        }

        byte[] salt;
        byte[] expectedHash;

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedHash = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length != SaltSize || expectedHash.Length != HashSize)
        {
            CryptographicOperations.ZeroMemory(expectedHash);
            return false;
        }

        byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

        try
        {
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(actualHash);
            CryptographicOperations.ZeroMemory(expectedHash);
        }
    }
}
