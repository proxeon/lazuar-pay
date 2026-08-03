// apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookSignature.cs
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Modules.One.Infrastructure.Workers;

/// <summary>
/// Standard Webhooks–style signing: <c>t={unix},v1={hmac_hex}</c> over <c>{timestamp}.{body}</c>.
/// Receivers should recompute HMAC and compare with a fixed-time equality check (see <see cref="TryVerify"/>).
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

    /// <summary>
    /// Verifies a Standard Webhooks–style signature header against the raw request body.
    /// </summary>
    /// <param name="secret">Endpoint signing secret (e.g. <c>whsec_…</c>).</param>
    /// <param name="body">Raw body bytes as UTF-8 string (must match what was signed).</param>
    /// <param name="headerValue">Value of the signature header (<c>t=…,v1=…</c>).</param>
    /// <param name="toleranceSeconds">
    /// Max allowed clock skew between <c>t</c> and <paramref name="nowUnixSeconds"/>; pass 0 to skip freshness check.
    /// </param>
    /// <param name="nowUnixSeconds">
    /// Current unix time for freshness; defaults to UTC now when null.
    /// </param>
    public static bool TryVerify(
        string secret,
        string body,
        string? headerValue,
        long toleranceSeconds = 300,
        long? nowUnixSeconds = null)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(headerValue))
        {
            return false;
        }

        if (!TryParseHeader(headerValue, out var timestamp, out var v1Hex))
        {
            return false;
        }

        if (toleranceSeconds > 0)
        {
            var now = nowUnixSeconds ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (Math.Abs(now - timestamp) > toleranceSeconds)
            {
                return false;
            }
        }

        var expected = ComputeHeaderValue(secret, body, timestamp);
        if (!TryParseHeader(expected, out _, out var expectedHex))
        {
            return false;
        }

        return FixedTimeEqualsHex(v1Hex, expectedHex);
    }

    internal static bool TryParseHeader(string headerValue, out long timestamp, out string v1Hex)
    {
        timestamp = 0;
        v1Hex = string.Empty;

        long? t = null;
        string? v1 = null;

        foreach (var part in headerValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            var key = part[..eq];
            var value = part[(eq + 1)..];

            if (key.Equals("t", StringComparison.OrdinalIgnoreCase)
                && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedTs))
            {
                t = parsedTs;
            }
            else if (key.Equals("v1", StringComparison.OrdinalIgnoreCase))
            {
                v1 = value;
            }
        }

        if (t is null || string.IsNullOrEmpty(v1))
        {
            return false;
        }

        timestamp = t.Value;
        v1Hex = v1;
        return true;
    }

    private static bool FixedTimeEqualsHex(string a, string b)
    {
        // Normalize for length / casing before byte compare.
        var left = Encoding.UTF8.GetBytes(a.ToLowerInvariant());
        var right = Encoding.UTF8.GetBytes(b.ToLowerInvariant());
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }
}
