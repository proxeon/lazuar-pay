using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lazuar.Pay.Data;
using Lazuar.Pay.Money;
using Lazuar.Pay.Secrets;

using Lazuar.Pay.Rails;

using Lazuar.Pay.Webhooks;

namespace Lazuar.Pay.Rails.Razorpay;

internal static class RazorpayWebhook
{
    public static PspParseResult Parse(string raw, IHeaderDictionary headers, GatewayCredentialRow cred, SecretBox box)
    {
        if (string.IsNullOrWhiteSpace(cred.WebhookCiphertext))
        {
            throw new InvalidOperationException("webhook secret missing");
        }

        var sigKey = headers.Keys.FirstOrDefault(k => k.Equals("X-Razorpay-Signature", StringComparison.OrdinalIgnoreCase));
        if (sigKey is null || !headers.TryGetValue(sigKey, out var signature) || string.IsNullOrWhiteSpace(signature))
        {
            throw new PspVerifyException("invalid signature");
        }

        var secret = box.Unprotect(cred.WebhookCiphertext);
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(raw));
        var expected = Convert.ToHexString(mac).ToLowerInvariant();
        var provided = signature.ToString().Trim().ToLowerInvariant();
        var left = Encoding.UTF8.GetBytes(provided);
        var right = Encoding.UTF8.GetBytes(expected);
        if (left.Length != right.Length || !CryptographicOperations.FixedTimeEquals(left, right))
        {
            throw new PspVerifyException("invalid signature");
        }

        using var doc = JsonDocument.Parse(raw);
        var eventType = doc.RootElement.TryGetProperty("event", out var ev) ? ev.GetString() : null;
        JsonElement entity = default;
        var hasEntity = doc.RootElement.TryGetProperty("payload", out var payload)
                        && payload.TryGetProperty("payment", out var payment)
                        && payment.TryGetProperty("entity", out entity);
        var paymentId = hasEntity && entity.TryGetProperty("id", out var pid) ? pid.GetString() : null;
        var headerEventId = Header(headers, "X-Razorpay-Event-Id");

        if (eventType == "payment.failed")
        {
            var failedId = headerEventId ?? (string.IsNullOrWhiteSpace(paymentId) ? null : "failed:" + paymentId);
            if (string.IsNullOrWhiteSpace(failedId))
            {
                throw new PspVerifyException("missing event id");
            }

            string? failedCheckout = null;
            if (hasEntity && entity.TryGetProperty("notes", out var failNotes) && failNotes.ValueKind == JsonValueKind.Object
                && failNotes.TryGetProperty("checkout_id", out var failCid))
            {
                failedCheckout = failCid.GetString();
            }

            return new PspParseResult
            {
                EventId = failedId,
                Failed = true,
                IgnoreReason = "payment_failed",
                CheckoutId = failedCheckout,
                ProviderRef = paymentId
            };
        }

        // Issue 004 (issues/003): Pay mints payment links, and Razorpay's own payment-links
        // docs point merchants at payment_link.paid — the event Pay used to file under
        // "ignored", so a merchant subscribing to exactly that event never saw a
        // fulfillment. The payment entity below carries everything the captured path reads
        // (id, minor amount, currency, notes, and payment_link.entity.id for session
        // binding). payment_link.expired stays ignored: the local TTL sweep owns expiry,
        // and a capture can still trail an expired link — that arrival must find the
        // late-pay route, which ignoring keeps reachable.
        if (eventType is not "payment.captured" and not "payment_link.paid")
        {
            var otherId = headerEventId
                          ?? (string.IsNullOrWhiteSpace(paymentId) ? (eventType ?? "razorpay") + ":none" : eventType + ":" + paymentId);
            return new PspParseResult { EventId = otherId, Ignored = true, IgnoreReason = eventType };
        }

        if (!hasEntity || string.IsNullOrWhiteSpace(paymentId))
        {
            throw new PspVerifyException("missing payment id");
        }

        if (!MoneyMath.TryNormalizeCurrency(
                entity.TryGetProperty("currency", out var cur) && cur.ValueKind == JsonValueKind.String ? cur.GetString() : null,
                out var currency))
        {
            throw new PspVerifyException("missing currency");
        }

        // Payment entity amount is already minor (paise/sen). RM10.00 → 1000.
        var amount = entity.TryGetProperty("amount", out var amt) && amt.ValueKind == JsonValueKind.Number
            ? amt.GetInt64()
            : 0L;

        string? checkoutId = null;
        if (entity.TryGetProperty("notes", out var notes) && notes.ValueKind == JsonValueKind.Object
            && notes.TryGetProperty("checkout_id", out var cid))
        {
            checkoutId = cid.GetString();
        }

        string? hostedSessionId = null;
        if (doc.RootElement.TryGetProperty("payload", out var pl)
            && pl.TryGetProperty("payment_link", out var link)
            && link.TryGetProperty("entity", out var linkEntity)
            && linkEntity.TryGetProperty("id", out var linkId))
        {
            hostedSessionId = linkId.GetString();
        }

        // Distinct namespace per success event type: a merchant enabling both events gets
        // two deliveries for one payment. Without the header event id they must not dedupe
        // against each other — the first fulfills and Handle answers the second benignly.
        var eventId = !string.IsNullOrWhiteSpace(headerEventId)
            ? headerEventId
            : (eventType == "payment_link.paid" ? "link_paid:" : "captured:") + paymentId;
        return new PspParseResult
        {
            EventId = eventId,
            CheckoutId = checkoutId,
            HostedSessionId = hostedSessionId,
            ProviderRef = paymentId,
            AmountMinor = amount,
            Currency = currency
        };
    }

    static string? Header(IHeaderDictionary headers, string name)
    {
        foreach (var key in headers.Keys)
        {
            if (key.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                var v = headers[key].ToString();
                return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
            }
        }

        return null;
    }
}
