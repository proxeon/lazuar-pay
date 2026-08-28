using System.Security.Cryptography;
using System.Text;
using Lazuar.Pay.Data;
using Lazuar.Pay.Money;
using Lazuar.Pay.Secrets;
using Microsoft.AspNetCore.WebUtilities;

using Lazuar.Pay.Rails;

using Lazuar.Pay.Webhooks;

namespace Lazuar.Pay.Rails.Billplz;

internal static class BillplzWebhook
{
    static readonly HashSet<string> ExtraFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "paid_at", "transaction_id", "transaction_status"
    };

    public static PspParseResult Parse(
        string raw,
        IQueryCollection query,
        GatewayCredentialRow cred,
        SecretBox box)
    {
        if (string.IsNullOrWhiteSpace(cred.WebhookCiphertext))
        {
            throw new InvalidOperationException("webhook secret missing");
        }

        var form = ParseForm(raw);
        if (!form.TryGetValue("x_signature", out var provided) || string.IsNullOrEmpty(provided))
        {
            throw new PspVerifyException("invalid signature");
        }

        var secret = box.Unprotect(cred.WebhookCiphertext);
        var withExtra = ComputeHmac(form, secret, excludeExtra: false);
        if (!FixedTimeEqualsHex(provided, withExtra))
        {
            var without = ComputeHmac(form, secret, excludeExtra: true);
            if (!FixedTimeEqualsHex(provided, without))
            {
                throw new PspVerifyException("invalid signature");
            }
        }

        var billId = form.GetValueOrDefault("id", "");
        if (string.IsNullOrWhiteSpace(billId))
        {
            throw new PspVerifyException("missing bill id");
        }

        var paid = form.GetValueOrDefault("paid", "false");
        var state = form.GetValueOrDefault("state", "due");
        var isPaid = paid.Equals("true", StringComparison.OrdinalIgnoreCase)
                     || state.Equals("paid", StringComparison.OrdinalIgnoreCase);
        if (!isPaid)
        {
            return new PspParseResult { EventId = "unpaid:" + billId, Ignored = true, IgnoreReason = "unpaid" };
        }

        var checkoutId = query["checkout_id"].ToString();
        if (string.IsNullOrWhiteSpace(checkoutId))
        {
            checkoutId = form.GetValueOrDefault("checkout_id", "");
        }

        if (string.IsNullOrWhiteSpace(checkoutId))
        {
            checkoutId = form.GetValueOrDefault("reference_1", "");
        }

        // Form paid_amount is sen (minor). RM10.00 → 1000.
        var paidCents = long.TryParse(form.GetValueOrDefault("paid_amount", "0"), out var pac) ? pac : 0L;
        if (!MoneyMath.TryNormalizeCurrency(form.GetValueOrDefault("currency", ""), out var currency))
        {
            throw new PspVerifyException("missing currency");
        }

        return new PspParseResult
        {
            EventId = "paid:" + billId,
            CheckoutId = string.IsNullOrWhiteSpace(checkoutId) ? null : checkoutId,
            HostedSessionId = billId,
            ProviderRef = billId,
            AmountMinor = paidCents,
            Currency = currency
        };
    }

    internal static string ComputeHmac(Dictionary<string, string> formData, string secretKey, bool excludeExtra)
    {
        var elements = formData
            .Where(kv =>
            {
                if (kv.Key.Equals("x_signature", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (excludeExtra && ExtraFields.Contains(kv.Key))
                {
                    return false;
                }

                return true;
            })
            .Select(kv => kv.Key + kv.Value)
            .OrderBy(e => e, StringComparer.Ordinal)
            .ToList();
        var source = string.Join("|", elements);
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secretKey), Encoding.UTF8.GetBytes(source));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    internal static Dictionary<string, string> ParseForm(string body)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in QueryHelpers.ParseQuery(body))
        {
            result[parameter.Key] = parameter.Value.ToString();
        }

        return result;
    }

    static bool FixedTimeEqualsHex(string provided, string computed)
    {
        var left = Encoding.UTF8.GetBytes(provided.Trim().ToLowerInvariant());
        var right = Encoding.UTF8.GetBytes(computed.Trim().ToLowerInvariant());
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }
}
