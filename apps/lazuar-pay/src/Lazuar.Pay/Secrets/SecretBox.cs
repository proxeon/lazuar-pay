using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Lazuar.Pay.Secrets;

/// <summary>AES-GCM wrap for BYOK. Key from Pay:WrapKey (32-byte base64). Never log plaintext.</summary>
public sealed class SecretBox(IConfiguration config, IHostEnvironment env)
{
    public string Protect(string plaintext)
    {
        var key = LoadKey();
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plain, cipher, tag);
        return Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray());
    }

    public string Unprotect(string wrapped)
    {
        var key = LoadKey();
        var raw = Convert.FromBase64String(wrapped);
        var nonce = raw[..12];
        var tag = raw[12..28];
        var cipher = raw[28..];
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }

    byte[] LoadKey()
    {
        var b64 = config["Pay:WrapKey"];
        if (string.IsNullOrWhiteSpace(b64))
        {
            if (env.IsProduction())
            {
                throw new InvalidOperationException("Pay:WrapKey is required in Production");
            }

            return SHA256.HashData("lazuar-pay-dev-wrap-key"u8.ToArray());
        }

        var key = Convert.FromBase64String(b64);
        if (key.Length != 32)
        {
            throw new InvalidOperationException("Pay:WrapKey must be 32 bytes base64");
        }

        return key;
    }
}
