using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using BuildingBlocks.Application;
using Microsoft.Extensions.Configuration;

namespace BuildingBlocks.Infrastructure;

/// <summary>
/// Shared AES-256-CBC secret vault. Key from <c>Kms:MasterKey</c>, falling back to <c>Jwt:Secret</c> for local/dev.
/// Stored format: base64(IV[16] + ciphertext).
/// </summary>
public sealed class AesSecretVault : ISecretVault
{
    private readonly byte[] _masterKey;

    public AesSecretVault(IConfiguration configuration)
    {
        var keyString = configuration["Kms:MasterKey"]
            ?? configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Kms:MasterKey (or Jwt:Secret fallback) configuration missing.");

        _masterKey = Encoding.UTF8.GetBytes(keyString.PadRight(32, '0')[..32]);
    }

    public string Encrypt(string plaintext)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);

        using var aes = Aes.Create();
        aes.Key = _masterKey;
        aes.GenerateIV();

        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        using var encryptor = aes.CreateEncryptor();
        var cipherText = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var finalBytes = new byte[aes.IV.Length + cipherText.Length];
        Buffer.BlockCopy(aes.IV, 0, finalBytes, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherText, 0, finalBytes, aes.IV.Length, cipherText.Length);

        return Convert.ToBase64String(finalBytes);
    }

    public string Decrypt(string ciphertextBase64)
    {
        ArgumentException.ThrowIfNullOrEmpty(ciphertextBase64);

        var passwordBytes = Convert.FromBase64String(ciphertextBase64);

        using var aes = Aes.Create();
        aes.Key = _masterKey;

        var iv = new byte[16];
        Array.Copy(passwordBytes, 0, iv, 0, 16);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream(passwordBytes, 16, passwordBytes.Length - 16);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var reader = new StreamReader(cs);

        return reader.ReadToEnd();
    }
}
