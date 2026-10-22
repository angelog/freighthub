using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Api.Infrastructure.Security;

/// <summary>
/// Criptografia autenticada AES-256-GCM. O payload persistido é
/// Base64(nonce[12] || tag[16] || ciphertext), permitindo decifrar sem estado externo.
/// </summary>
public sealed class AesGcmCredentialProtector : ICredentialProtector
{
    private const int NonceSize = 12; // 96 bits, recomendado para GCM
    private const int TagSize = 16;   // 128 bits
    private readonly byte[] _key;

    public AesGcmCredentialProtector(IOptions<CredentialProtectionOptions> options)
    {
        var configured = options.Value.EncryptionKey;
        if (string.IsNullOrWhiteSpace(configured))
            throw new InvalidOperationException(
                "Credentials:EncryptionKey não configurada. Defina uma chave Base64 de 32 bytes " +
                "(ex.: variável de ambiente Credentials__EncryptionKey).");

        byte[] key;
        try
        {
            key = Convert.FromBase64String(configured);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Credentials:EncryptionKey deve estar em Base64 válido.");
        }

        if (key.Length != 32)
            throw new InvalidOperationException(
                $"Credentials:EncryptionKey deve ter 32 bytes (256 bits); recebeu {key.Length}.");

        _key = key;
    }

    public string Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        var payload = new byte[NonceSize + TagSize + cipher.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, payload, NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, payload, NonceSize + TagSize, cipher.Length);

        return Convert.ToBase64String(payload);
    }

    public string Unprotect(string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        var raw = Convert.FromBase64String(payload);
        if (raw.Length < NonceSize + TagSize)
            throw new CryptographicException("Payload de credencial malformado.");

        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var cipher = new byte[raw.Length - NonceSize - TagSize];

        Buffer.BlockCopy(raw, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(raw, NonceSize, tag, 0, TagSize);
        Buffer.BlockCopy(raw, NonceSize + TagSize, cipher, 0, cipher.Length);

        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);

        return Encoding.UTF8.GetString(plain);
    }
}
