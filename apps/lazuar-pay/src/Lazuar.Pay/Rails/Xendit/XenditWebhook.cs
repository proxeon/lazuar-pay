using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lazuar.Pay.Data;
using Lazuar.Pay.Money;
using Lazuar.Pay.Secrets;

using Lazuar.Pay.Rails;

using Lazuar.Pay.Webhooks;

namespace Lazuar.Pay.Rails.Xendit;

internal static class XenditWebhook
{
    public static PspParseResult Parse(string raw, IHeaderDictionary headers, GatewayCredentialRow cred, SecretBox box)
    {
        if (string.IsNullOrWhiteSpace(cred.WebhookCiphertext))
        {
            throw new InvalidOperationException("webhook secret missing");
        }

        var expected = box.Unprotect(cred.WebhookCiphertext);
        var provided = "";
        foreach (var key in headers.Keys)
        {
            if (key.Equals("x-callback-token", StringComparison.OrdinalIgnoreCase))
            {
                provided = headers[key].ToString();
                break;
            }
        }

        // Hash first so token length is not a timing oracle (Hub 073 judgment).
        var left = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        var right = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        if (!CryptographicOperations.FixedTimeEquals(left, right))
        {
            throw new PspVerifyException("invalid signature");
        }

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        var invoice = root;
        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
        {
            invoice = data;
        }

        var status = ReadString(invoice, "status") ?? ReadString(root, "event") ?? "";
        var invoiceId = ReadString(invoice, "id");
        if (string.IsNullOrWhiteSpace(invoiceId))
        {
            throw new PspVerifyException("missing invoice id");
        }

        if (status.Equals("SETTLED", StringComparison.OrdinalIgnoreCase)
            || status.Equals("invoice.settled", StringComparison.OrdinalIgnoreCase))
        {
            return new PspParseResult { EventId = "settled:" + invoiceId, Ignored = true, IgnoreReason = "settled" };
        }

        var paid = status.Equals("PAID", StringComparison.OrdinalIgnoreCase)
                   || status.Equals("invoice.paid", StringComparison.OrdinalIgnoreCase);
        if (!paid)
        {
            return new PspParseResult { EventId = status + ":" + invoiceId, Ignored = true, IgnoreReason = status };
        }

        if (!MoneyMath.TryNormalizeCurrency(ReadString(invoice, "currency"), out var currency))
        {
            throw new PspVerifyException("missing currency");
        }

        // Invoice paid_amount is major units (10.00), then ToMinor for match.
        decimal amount = 0;
        if (invoice.TryGetProperty("paid_amount", out var paidAmt) && paidAmt.ValueKind == JsonValueKind.Number)
        {
            amount = paidAmt.GetDecimal();
        }
        else if (invoice.TryGetProperty("amount", out var amt) && amt.ValueKind == JsonValueKind.Number)
        {
            amount = amt.GetDecimal();
        }

        string? checkoutId = null;
        if (invoice.TryGetProperty("metadata", out var meta) && meta.ValueKind == JsonValueKind.Object
            && meta.TryGetProperty("checkout_id", out var cid))
        {
            checkoutId = cid.GetString();
        }

        checkoutId ??= ReadString(invoice, "external_id");

        return new PspParseResult
        {
            EventId = "paid:" + invoiceId,
            CheckoutId = checkoutId,
            HostedSessionId = invoiceId,
            ProviderRef = invoiceId,
            AmountMinor = MoneyMath.ToMinor(amount),
            Currency = currency
        };
    }

    static string? ReadString(JsonElement el, string name) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;
}
