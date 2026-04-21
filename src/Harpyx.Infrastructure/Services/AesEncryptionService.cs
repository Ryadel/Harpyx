using System.Security.Cryptography;
using Harpyx.Application.Interfaces;

namespace Harpyx.Infrastructure.Services;

/// <summary>
/// AES-GCM encryption at rest for API keys.
/// Master key is loaded from environment variable LLM_ENCRYPTION_KEY (base64, 32 bytes).
/// </summary>
public sealed class AesEncryptionService : IEncryptionService
{
    private readonly byte[] _masterKey;

    public AesEncryptionService(EncryptionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.MasterKey))
            throw new InvalidOperationException(
                "LLM_ENCRYPTION_KEY environment variable is not set. " +
                "Generate a 32-byte key: Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))");

        _masterKey = Convert.FromBase64String(options.MasterKey);
        if (_masterKey.Length != 32)
            throw new InvalidOperationException("LLM_ENCRYPTION_KEY must be exactly 32 bytes (256-bit) base64-encoded.");
    }

    public string Encrypt(string plaintext)
    {
        var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize); // 12 bytes
        var tag = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 bytes
        var ciphertext = new byte[plaintextBytes.Length];

        using var aes = new AesGcm(_masterKey, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // Format: base64(nonce + ciphertext + tag)
        var result = new byte[nonce.Length + ciphertext.Length + tag.Length];
        nonce.CopyTo(result, 0);
        ciphertext.CopyTo(result, nonce.Length);
        tag.CopyTo(result, nonce.Length + ciphertext.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string encryptedBase64)
    {
        var data = Convert.FromBase64String(encryptedBase64);

        var nonceSize = AesGcm.NonceByteSizes.MaxSize; // 12
        var tagSize = AesGcm.TagByteSizes.MaxSize; // 16
        var ciphertextSize = data.Length - nonceSize - tagSize;

        if (ciphertextSize < 0)
            throw new CryptographicException("Invalid encrypted data.");

        var nonce = data.AsSpan(0, nonceSize);
        var ciphertext = data.AsSpan(nonceSize, ciphertextSize);
        var tag = data.AsSpan(nonceSize + ciphertextSize, tagSize);

        var plaintext = new byte[ciphertextSize];
        using var aes = new AesGcm(_masterKey, tagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return System.Text.Encoding.UTF8.GetString(plaintext);
    }
}

public class EncryptionOptions
{
    public string MasterKey { get; set; } = string.Empty;
}
