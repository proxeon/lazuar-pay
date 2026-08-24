using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Lazuar.Pay.Identity.OneWebhooks;

/// <summary>
/// Standard Webhooks–style verify: header t={unix},v1={lowercase hex} over {unix}.{body}.
/// Judgment stolen from One's signer. Do not import the Hub worker type.
/// </summary>
internal static class OneWebhookSignature
{
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

        var signedPayload = $"{timestamp}.{body}";
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signedPayload));
        var expectedHex = Convert.ToHexString(hash).ToLowerInvariant();
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

    static bool FixedTimeEqualsHex(string a, string b)
    {
        var left = Encoding.UTF8.GetBytes(a.ToLowerInvariant());
        var right = Encoding.UTF8.GetBytes(b.ToLowerInvariant());
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }
}
