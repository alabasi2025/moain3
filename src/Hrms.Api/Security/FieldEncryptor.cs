using System.Security.Cryptography;
using System.Text;

namespace Hrms.Api.Security;

public sealed class FieldEncryptor(string base64Key32)
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;

    private readonly byte[] _key = ParseKey(base64Key32);

    public string? Encrypt(string? plain)
    {
        if (plain is null)
        {
            return null;
        }

        byte[] plaintext = Encoding.UTF8.GetBytes(plain);

        try
        {
            byte[] payload = new byte[checked(NonceSize + TagSize + plaintext.Length)];
            Span<byte> nonce = payload.AsSpan(0, NonceSize);
            Span<byte> tag = payload.AsSpan(NonceSize, TagSize);
            Span<byte> ciphertext = payload.AsSpan(NonceSize + TagSize);

            RandomNumberGenerator.Fill(nonce);

            using var aes = new AesGcm(_key, TagSize);
            aes.Encrypt(nonce, plaintext, ciphertext, tag);

            return Convert.ToBase64String(payload);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public string? Decrypt(string? cipher)
    {
        if (cipher is null)
        {
            return null;
        }

        byte[] payload = Convert.FromBase64String(cipher);

        if (payload.Length < NonceSize + TagSize)
        {
            throw new FormatException("The encrypted field payload is invalid.");
        }

        ReadOnlySpan<byte> nonce = payload.AsSpan(0, NonceSize);
        ReadOnlySpan<byte> tag = payload.AsSpan(NonceSize, TagSize);
        ReadOnlySpan<byte> ciphertext = payload.AsSpan(NonceSize + TagSize);
        byte[] plaintext = new byte[ciphertext.Length];

        try
        {
            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);

            return Encoding.UTF8.GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static byte[] ParseKey(string base64Key32)
    {
        ArgumentNullException.ThrowIfNull(base64Key32);

        byte[] key;

        try
        {
            key = Convert.FromBase64String(base64Key32);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException(
                "The encryption key must be a Base64-encoded 32-byte key.",
                nameof(base64Key32),
                exception);
        }

        if (key.Length != KeySize)
        {
            CryptographicOperations.ZeroMemory(key);

            throw new ArgumentException(
                "The encryption key must contain exactly 32 bytes.",
                nameof(base64Key32));
        }

        return key;
    }
}
