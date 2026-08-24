using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lazuar.Pay.Data;
using Lazuar.Pay.Money;
using Lazuar.Pay.Secrets;

namespace Lazuar.Pay.Gateways;

internal static class ChipWebhook
{
    public static PspParseResult Parse(string raw, IHeaderDictionary headers, GatewayCredentialRow cred, SecretBox box)
    {
        var sigKey = headers.Keys.FirstOrDefault(k => k.Equals("X-Signature", StringComparison.OrdinalIgnoreCase));
        if (sigKey is null || !headers.TryGetValue(sigKey, out var sig) || string.IsNullOrWhiteSpace(sig))
        {
            throw new PspVerifyException("invalid signature");
        }

        if (string.IsNullOrWhiteSpace(cred.WebhookCiphertext))
        {
            throw new InvalidOperationException("webhook secret missing");
        }

        var pem = box.Unprotect(cred.WebhookCiphertext);
        byte[] signatureBytes;
        try
        {
            signatureBytes = Convert.FromBase64String(sig.ToString());
        }
        catch (FormatException)
        {
            throw new PspVerifyException("invalid signature");
        }

        using var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(pem);
        }
        catch (Exception)
        {
            throw new PspVerifyException("invalid signature");
        }

        var ok = rsa.VerifyData(Encoding.UTF8.GetBytes(raw), signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        if (!ok)
        {
            throw new PspVerifyException("invalid signature");
        }

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        var eventType = root.TryGetProperty("event_type", out var et) ? et.GetString() : null;
        var purchaseId = ReadStablePurchaseId(root);
        if (string.IsNullOrWhiteSpace(purchaseId))
        {
            throw new PspVerifyException("missing purchase id");
        }

        if (eventType == "purchase.preauthorized")
        {
            return new PspParseResult { EventId = "preauth:" + purchaseId, Ignored = true, IgnoreReason = "preauthorized" };
        }

        if (eventType == "purchase.payment_failure")
        {
            return new PspParseResult { EventId = "failed:" + purchaseId, Ignored = true, IgnoreReason = "payment_failure" };
        }

        if (eventType != "purchase.paid")
        {
            return new PspParseResult { EventId = (eventType ?? "chip") + ":" + purchaseId, Ignored = true, IgnoreReason = eventType };
        }

        var purchase = root.TryGetProperty("purchase", out var p) && p.ValueKind == JsonValueKind.Object ? p : default;
        // CHIP purchase.total is sen/cents. RM10.00 → 1000. Do not divide by 100.
        var total = purchase.ValueKind == JsonValueKind.Object && purchase.TryGetProperty("total", out var t) ? t.GetDecimal() : 0m;
        var rawCurrency = purchase.ValueKind == JsonValueKind.Object && purchase.TryGetProperty("currency", out var c)
            ? c.GetString()
            : null;
        if (!MoneyMath.TryNormalizeCurrency(rawCurrency, out var currency))
        {
            throw new PspVerifyException("missing currency");
        }

        string? checkoutId = null;
        if (purchase.ValueKind == JsonValueKind.Object
            && purchase.TryGetProperty("metadata", out var meta)
            && meta.ValueKind == JsonValueKind.Object
            && meta.TryGetProperty("checkout_id", out var cid))
        {
            checkoutId = cid.GetString();
        }

        return new PspParseResult
        {
            EventId = "paid:" + purchaseId,
            CheckoutId = checkoutId,
            ProviderRef = purchaseId,
            AmountMinor = (long)total,
            Currency = currency
        };
    }

    static string? ReadStablePurchaseId(JsonElement root)
    {
        if (root.TryGetProperty("purchase", out var purchase) && purchase.ValueKind == JsonValueKind.Object
            && purchase.TryGetProperty("id", out var nested) && nested.ValueKind == JsonValueKind.String)
        {
            var id = nested.GetString();
            if (!string.IsNullOrWhiteSpace(id))
            {
                return id;
            }
        }

        return root.TryGetProperty("id", out var top) && top.ValueKind == JsonValueKind.String ? top.GetString() : null;
    }
}
