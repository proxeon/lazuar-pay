using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Configuration;
using Modules.Lhdn.Application.Services;

namespace Modules.Lhdn.Infrastructure.Services;

/// <summary>
/// Encrypts PFX payload bytes and passphrase at rest (AES-256-CBC, base64 IV+ciphertext).
/// Legacy rows may still store raw base64 PFX with only the password encrypted.
/// </summary>
public class CertificateVaultService : ICertificateVaultService
{
    private readonly byte[] _masterKey;

    public CertificateVaultService(IConfiguration configuration)
    {
        var keyString = FirstNonEmpty(configuration["Kms:MasterKey"], configuration["Jwt:Secret"])
            ?? throw new InvalidOperationException("Kms:MasterKey (or Jwt:Secret fallback) configuration missing.");
        _masterKey = Encoding.UTF8.GetBytes(keyString.PadRight(32, '0')[..32]);
    }

    public X509Certificate2 GetDecryptedCertificate(string encryptedPfxBase64, string pfxPasswordCiphertext)
    {
        var rawPassword = DecryptString(pfxPasswordCiphertext);

        // Prefer AES-encrypted PFX; fall back to legacy raw base64 PFX.
        try
        {
            var pfxBytes = DecryptBytes(encryptedPfxBase64);
            return LoadPkcs12(pfxBytes, rawPassword);
        }
        catch
        {
            var pfxBytes = Convert.FromBase64String(encryptedPfxBase64);
            return LoadPkcs12(pfxBytes, rawPassword);
        }
    }

    public (string EncryptedPfx, string CipherText) EncryptCertificate(string base64P12, string passphrase)
    {
        ArgumentException.ThrowIfNullOrEmpty(base64P12);
        ArgumentException.ThrowIfNullOrEmpty(passphrase);

        var pfxBytes = Convert.FromBase64String(base64P12);
        var encryptedPfx = EncryptBytes(pfxBytes);
        var passwordCipher = EncryptString(passphrase);

        return (encryptedPfx, passwordCipher);
    }

    private static X509Certificate2 LoadPkcs12(byte[] pfxBytes, string rawPassword) =>
        // Cross-Platform compliance (macOS/Linux reject EphemeralKeySet)
        X509CertificateLoader.LoadPkcs12(
            pfxBytes,
            rawPassword,
            X509KeyStorageFlags.DefaultKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);

    private string EncryptString(string plaintext)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        return EncryptBytes(plainBytes);
    }

    private string DecryptString(string ciphertextBase64)
    {
        var plainBytes = DecryptBytes(ciphertextBase64);
        return Encoding.UTF8.GetString(plainBytes);
    }

    private string EncryptBytes(byte[] plainBytes)
    {
        using var aes = Aes.Create();
        aes.Key = _masterKey;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var cipherText = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var finalBytes = new byte[aes.IV.Length + cipherText.Length];
        Buffer.BlockCopy(aes.IV, 0, finalBytes, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherText, 0, finalBytes, aes.IV.Length, cipherText.Length);

        return Convert.ToBase64String(finalBytes);
    }

    private byte[] DecryptBytes(string ciphertextBase64)
    {
        var passwordBytes = Convert.FromBase64String(ciphertextBase64);

        using var aes = Aes.Create();
        aes.Key = _masterKey;

        var iv = new byte[16];
        Array.Copy(passwordBytes, 0, iv, 0, 16);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(passwordBytes, 16, passwordBytes.Length - 16);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
            {
                return v;
            }
        }

        return null;
    }
}
