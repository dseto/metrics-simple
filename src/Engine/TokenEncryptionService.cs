using System.Security.Cryptography;

namespace Metrics.Engine;

/// <summary>
/// Provides AES-256-GCM encryption/decryption for API tokens.
/// Per spec: uses METRICS_SECRET_KEY env var (32 bytes base64).
/// </summary>
public interface ITokenEncryptionService
{
    /// <summary>
    /// Encrypts plaintext token using AES-256-GCM.
    /// Returns (nonce, ciphertext+tag) as base64 strings.
    /// </summary>
    EncryptedToken Encrypt(string plaintext);

    /// <summary>
    /// Decrypts ciphertext+tag using AES-256-GCM.
    /// Throws if authentication fails or key missing.
    /// </summary>
    string Decrypt(string nonceBase64, string ciphertextBase64);
}

public record EncryptedToken(
    int Version,
    string Algorithm,
    string NonceBase64,
    string CiphertextBase64
);

public sealed class TokenEncryptionService : ITokenEncryptionService
{
    private const int KeySizeBytes = 32; // AES-256
    private const int NonceSizeBytes = 12; // GCM standard
    private const int TagSizeBytes = 16; // GCM standard

    private readonly byte[] _key;

    public TokenEncryptionService()
    {
        var keyBase64 = Environment.GetEnvironmentVariable("METRICS_SECRET_KEY");
        if (string.IsNullOrWhiteSpace(keyBase64))
        {
            throw new InvalidOperationException(
                "METRICS_SECRET_KEY environment variable not configured. " +
                "Required for token encryption. Set to a 32-byte base64 key.");
        }

        try
        {
            _key = Convert.FromBase64String(keyBase64);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                "METRICS_SECRET_KEY is not a valid base64 string. " +
                "Generate with: openssl rand -base64 32");
        }

        if (_key.Length != KeySizeBytes)
        {
            throw new InvalidOperationException(
                $"METRICS_SECRET_KEY must be exactly {KeySizeBytes} bytes (base64: 44 chars). " +
                "Generate with: openssl rand -base64 32");
        }
    }

    public EncryptedToken Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            throw new ArgumentException("Plaintext cannot be null or empty", nameof(plaintext));

        // Generate random nonce (12 bytes)
        var nonce = new byte[NonceSizeBytes];
        RandomNumberGenerator.Fill(nonce);

        // Prepare plaintext bytes
        var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);

        // Prepare ciphertext buffer (plaintext + tag)
        var ciphertextWithTag = new byte[plaintextBytes.Length + TagSizeBytes];
        var tag = new byte[TagSizeBytes];

        // Encrypt using AES-256-GCM
        using var aesGcm = new AesGcm(_key, TagSizeBytes);
        aesGcm.Encrypt(nonce, plaintextBytes, ciphertextWithTag.AsSpan(0, plaintextBytes.Length), tag);

        // Append tag to ciphertext
        tag.CopyTo(ciphertextWithTag, plaintextBytes.Length);

        return new EncryptedToken(
            Version: 1,
            Algorithm: "AES-256-GCM",
            NonceBase64: Convert.ToBase64String(nonce),
            CiphertextBase64: Convert.ToBase64String(ciphertextWithTag)
        );
    }

    public string Decrypt(string nonceBase64, string ciphertextBase64)
    {
        if (string.IsNullOrEmpty(nonceBase64))
            throw new ArgumentException("Nonce cannot be null or empty", nameof(nonceBase64));
        if (string.IsNullOrEmpty(ciphertextBase64))
            throw new ArgumentException("Ciphertext cannot be null or empty", nameof(ciphertextBase64));

        byte[] nonce;
        byte[] ciphertextWithTag;

        try
        {
            nonce = Convert.FromBase64String(nonceBase64);
            ciphertextWithTag = Convert.FromBase64String(ciphertextBase64);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("Invalid base64 encoding in encrypted data", ex);
        }

        if (nonce.Length != NonceSizeBytes)
            throw new CryptographicException($"Invalid nonce size: expected {NonceSizeBytes}, got {nonce.Length}");

        if (ciphertextWithTag.Length < TagSizeBytes)
            throw new CryptographicException($"Ciphertext too short (must include {TagSizeBytes}-byte tag)");

        // Split ciphertext and tag
        var ciphertextLength = ciphertextWithTag.Length - TagSizeBytes;
        var ciphertext = ciphertextWithTag.AsSpan(0, ciphertextLength);
        var tag = ciphertextWithTag.AsSpan(ciphertextLength, TagSizeBytes);

        // Decrypt
        var plaintextBytes = new byte[ciphertextLength];
        using var aesGcm = new AesGcm(_key, TagSizeBytes);

        try
        {
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintextBytes);
        }
        catch (CryptographicException ex)
        {
            throw new CryptographicException("Decryption failed (authentication failed or corrupted data)", ex);
        }

        return System.Text.Encoding.UTF8.GetString(plaintextBytes);
    }
}
