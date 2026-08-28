using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lazuar.Pay.Money;
using Lazuar.Pay.Webhooks;

namespace Lazuar.Pay.Rails.Test;

internal static class TestWebhook
{
    public const string SignatureHeader = "X-Pay-Test-Signature";

    public static PspParseResult Parse(string json, IHeaderDictionary headers, IConfiguration config)
    {
        var secret = config["Pay:TestWebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("webhook secret missing");
        }

        if (!headers.TryGetValue(SignatureHeader, out var provided) || string.IsNullOrWhiteSpace(provided))
        {
            throw new PspVerifyException("invalid signature");
        }

        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(json));
        var expected = Convert.ToHexString(mac).ToLowerInvariant();
        var got = provided.ToString().Trim().ToLowerInvariant();
        var left = Encoding.UTF8.GetBytes(expected);
        var right = Encoding.UTF8.GetBytes(got);
        if (left.Length != right.Length || !CryptographicOperations.FixedTimeEquals(left, right))
        {
            throw new PspVerifyException("invalid signature");
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            throw new PspVerifyException("invalid event");
        }

        using (doc)
        {
            var root = doc.RootElement;
            var eventId = root.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String
                ? idEl.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(eventId))
            {
                throw new PspVerifyException("missing event id");
            }

            string? checkoutId = null;
            if (root.TryGetProperty("checkout_id", out var checkoutEl) && checkoutEl.ValueKind == JsonValueKind.String)
            {
                checkoutId = checkoutEl.GetString();
            }

            if (string.IsNullOrWhiteSpace(checkoutId))
            {
                throw new PspVerifyException("missing checkout id");
            }

            var failed = root.TryGetProperty("status", out var statusEl)
                && statusEl.ValueKind == JsonValueKind.String
                && string.Equals(statusEl.GetString(), "failed", StringComparison.OrdinalIgnoreCase);

            long? amount = null;
            if (root.TryGetProperty("amount_total", out var amountEl) && amountEl.TryGetInt64(out var parsedAmount))
            {
                amount = parsedAmount;
            }
            else if (!failed)
            {
                throw new PspVerifyException("missing amount");
            }

            string? currency = null;
            if (root.TryGetProperty("currency", out var ccyEl) && ccyEl.ValueKind == JsonValueKind.String)
            {
                MoneyMath.TryNormalizeCurrency(ccyEl.GetString(), out currency);
            }

            if (!failed && string.IsNullOrWhiteSpace(currency))
            {
                throw new PspVerifyException("missing currency");
            }

            return new PspParseResult
            {
                EventId = eventId,
                CheckoutId = checkoutId,
                ProviderRef = eventId,
                AmountMinor = amount,
                Currency = currency,
                Failed = failed,
                IgnoreReason = failed ? "payment_failed" : null
            };
        }
    }
}
