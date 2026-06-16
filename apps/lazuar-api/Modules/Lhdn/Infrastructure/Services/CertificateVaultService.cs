using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Configuration;
using Modules.Lhdn.Application.Services;

namespace Modules.Lhdn.Infrastructure.Services;

/// <summary>
/// Simulates an Envelope Encryption flow using a Cloud KMS Master Key.
/// Decrypts the tenant's PKI certificate securely in memory without writing to disk.
/// </summary>
public class CertificateVaultService : ICertificateVaultService
{
    private readonly byte[] _masterKey;

    public CertificateVaultService(IConfiguration configuration)
    {
        var keyString = configuration["Kms:MasterKey"] ?? throw new InvalidOperationException("Kms:MasterKey configuration missing.");
        _masterKey = Encoding.UTF8.GetBytes(keyString.PadRight(32, '0')[..32]); 
    }

    public X509Certificate2 GetDecryptedCertificate(string encryptedPfxBase64, string pfxPasswordCiphertext)
    {
        var pfxBytes = Convert.FromBase64String(encryptedPfxBase64);
        var passwordBytes = Convert.FromBase64String(pfxPasswordCiphertext);

        using var aes = Aes.Create();
        aes.Key = _masterKey;
        
        // The IV was prepended to the ciphertext during encryption.
        // We slice the first 16 bytes here to retrieve it safely.
        var iv = new byte[16];
        Array.Copy(passwordBytes, 0, iv, 0, 16);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream(passwordBytes, 16, passwordBytes.Length - 16);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var reader = new StreamReader(cs);
        
        var rawPassword = reader.ReadToEnd();

        return X509CertificateLoader.LoadPkcs12(pfxBytes, rawPassword, X509KeyStorageFlags.EphemeralKeySet);
    }
}
