using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Modules.Communications.Infrastructure.Security;

/// <summary>
/// Svix/Resend webhook signatures: HMAC-SHA256 over <c>{id}.{timestamp}.{body}</c>
/// with the base64 payload after <c>whsec_</c>.
/// </summary>
public static class SvixWebhookSignature
{
    public static byte[] ResolveKey(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
            throw new ArgumentException("Webhook secret is required.", nameof(secret));

        var material = secret.StartsWith("whsec_", StringComparison.Ordinal)
            ? secret["whsec_".Length..]
            : secret;

        return Convert.FromBase64String(material);
    }

    public static string Sign(string secret, string svixId, string svixTimestamp, string rawBody)
    {
        var signed = $"{svixId}.{svixTimestamp}.{rawBody}";
        var hash = HMACSHA256.HashData(ResolveKey(secret), Encoding.UTF8.GetBytes(signed));
        return Convert.ToBase64String(hash);
    }

    public static bool IsValid(string secret, string svixId, string svixTimestamp, string rawBody, string svixSignatureHeader)
    {
        var expected = Sign(secret, svixId, svixTimestamp, rawBody);
        var received = svixSignatureHeader
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.StartsWith("v1=", StringComparison.Ordinal) ? part["v1=".Length..] : null)
            .FirstOrDefault(v => v != null);

        if (received == null)
            return false;

        var expectedBytes = Encoding.ASCII.GetBytes(expected);
        var receivedBytes = Encoding.ASCII.GetBytes(received);
        return expectedBytes.Length == receivedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, receivedBytes);
    }
}
