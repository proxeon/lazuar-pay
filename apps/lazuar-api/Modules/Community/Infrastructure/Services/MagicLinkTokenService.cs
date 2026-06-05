using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Modules.Community.Application;

namespace Modules.Community.Infrastructure.Services;

public class MagicLinkTokenService : IMagicLinkTokenService
{
    private readonly string _secret;

    public MagicLinkTokenService(IConfiguration configuration)
    {
        _secret = configuration["Jwt:Secret"] ?? "fallback_dev_secret_key";
    }

    public string GenerateToken(Guid subscriptionId)
    {
        var expiry = DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds();
        var payload = $"{subscriptionId}:{expiry}";
        var hash = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(_secret),
            Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

        var tokenString = $"{payload}:{hash}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(tokenString));
    }

    public Guid? ValidateToken(string token)
    {
        try 
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var parts = decoded.Split(':');
            if (parts.Length != 3) return null;
            if (!Guid.TryParse(parts[0], out var subId)) return null;
            if (!long.TryParse(parts[1], out var expiry)) return null;
            
            var expectedHash = Convert.ToHexString(HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(_secret),
                Encoding.UTF8.GetBytes($"{subId}:{expiry}"))).ToLowerInvariant();

            if (parts[2] != expectedHash) return null;
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiry) return null;

            return subId;
        } 
        catch 
        { 
            return null; 
        }
    }
}
