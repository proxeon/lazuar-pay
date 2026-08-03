// apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookSignature.cs
using System.Security.Cryptography;
using System.Text;

namespace Modules.One.Infrastructure.Workers;

/// <summary>
/// Standard Webhooks–style signing: <c>t={unix},v1={hmac_hex}</c> over <c>{timestamp}.{body}</c>.
/// </summary>
public static class OutboundWebhookSignature
{
    public static string ComputeHeaderValue(string secret, string body, long unixTimestampSeconds)
    {
        var signedPayload = $"{unixTimestampSeconds}.{body}";
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(signedPayload);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        return $"t={unixTimestampSeconds},v1={hex}";
    }
}
