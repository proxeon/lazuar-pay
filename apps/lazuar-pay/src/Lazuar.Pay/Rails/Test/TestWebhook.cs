using System.Text.Json;
using Lazuar.Pay.Money;
using Lazuar.Pay.Webhooks;

namespace Lazuar.Pay.Rails.Test;

internal static class TestWebhook
{
    public static PspParseResult Parse(string json)
    {
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
                eventId = "test:" + Guid.NewGuid().ToString("N");
            }

            string? checkoutId = null;
            if (root.TryGetProperty("checkout_id", out var checkoutEl) && checkoutEl.ValueKind == JsonValueKind.String)
            {
                checkoutId = checkoutEl.GetString();
            }

            long? amountMinor = null;
            if (root.TryGetProperty("amount_total", out var amountEl) && amountEl.TryGetInt64(out var amount))
            {
                amountMinor = amount;
            }

            string? currency = null;
            if (root.TryGetProperty("currency", out var ccyEl) && ccyEl.ValueKind == JsonValueKind.String)
            {
                MoneyMath.TryNormalizeCurrency(ccyEl.GetString(), out currency);
            }

            return new PspParseResult
            {
                EventId = eventId,
                CheckoutId = checkoutId,
                ProviderRef = eventId,
                AmountMinor = amountMinor,
                Currency = currency
            };
        }
    }
}
