using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Configuration;
using Modules.Lhdn.Application.Services;

namespace Modules.Lhdn.Infrastructure.Services;

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
        
        var iv = new byte[16];
        Array.Copy(passwordBytes, 0, iv, 0, 16);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream(passwordBytes, 16, passwordBytes.Length - 16);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var reader = new StreamReader(cs);
        
        var rawPassword = reader.ReadToEnd();

        // Cross-Platform compliance (macOS/Linux reject EphemeralKeySet)
        return X509CertificateLoader.LoadPkcs12(
            pfxBytes, 
            rawPassword, 
            X509KeyStorageFlags.DefaultKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
    }

    public (string EncryptedPfx, string CipherText) EncryptCertificate(string base64P12, string passphrase)
    {
        using var aes = Aes.Create();
        aes.Key = _masterKey;
        aes.GenerateIV();

        var passwordBytes = Encoding.UTF8.GetBytes(passphrase);
        using var encryptor = aes.CreateEncryptor();
        var cipherText = encryptor.TransformFinalBlock(passwordBytes, 0, passwordBytes.Length);

        var finalBytes = new byte[aes.IV.Length + cipherText.Length];
        Buffer.BlockCopy(aes.IV, 0, finalBytes, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherText, 0, finalBytes, aes.IV.Length, cipherText.Length);

        return (base64P12, Convert.ToBase64String(finalBytes));
    }
}
