using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Modules.Commerce.Contracts;

namespace Modules.Commerce.Infrastructure.Security;

/// <summary>
/// HMAC-SHA256 portal tokens: Base64url("{subscriptionId}:{expiryUnix}:{hmacHex}"), 24h TTL.
/// Validate also accepts legacy standard Base64 (in-flight emails).
/// Secret source: Jwt:Secret (parity with pre-move BB impl — do not change without deliberate token versioning).
/// </summary>
public class MagicLinkTokenService : IMagicLinkTokenService
{
    private readonly string _secret;

    public MagicLinkTokenService(IConfiguration configuration)
    {
        var secret = configuration["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("Jwt:Secret is required to mint and validate portal magic-link tokens.");
        }

        _secret = secret;
    }

    public string GenerateToken(Guid subscriptionId)
    {
        var expiry = DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds();
        var payload = $"{subscriptionId}:{expiry}";
        var hash = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(_secret),
            Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

        var tokenString = $"{payload}:{hash}";
        return ToBase64Url(Encoding.UTF8.GetBytes(tokenString));
    }

    public Guid? ValidateToken(string token)
    {
        try
        {
            var decoded = Encoding.UTF8.GetString(FromBase64UrlOrStd(token));
            var parts = decoded.Split(':');
            if (parts.Length != 3) return null;
            if (!Guid.TryParse(parts[0], out var subId)) return null;
            if (!long.TryParse(parts[1], out var expiry)) return null;

            var expectedHash = Convert.ToHexString(HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(_secret),
                Encoding.UTF8.GetBytes($"{subId}:{expiry}"))).ToLowerInvariant();

            var provided = Encoding.UTF8.GetBytes(parts[2]);
            var expected = Encoding.UTF8.GetBytes(expectedHash);
            if (provided.Length != expected.Length
                || !CryptographicOperations.FixedTimeEquals(provided, expected))
            {
                return null;
            }

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiry) return null;

            return subId;
        }
        catch
        {
            return null;
        }
    }

    internal static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    internal static byte[] FromBase64UrlOrStd(string token)
    {
        // In-flight emails still carry standard Base64 (padding / + /).
        try
        {
            return Convert.FromBase64String(token);
        }
        catch (FormatException)
        {
            var std = token.Replace('-', '+').Replace('_', '/');
            switch (std.Length % 4)
            {
                case 2: std += "=="; break;
                case 3: std += "="; break;
            }

            return Convert.FromBase64String(std);
        }
    }
}
