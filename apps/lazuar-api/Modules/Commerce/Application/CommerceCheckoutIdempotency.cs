using System;
using System.Security.Cryptography;
using System.Text;

namespace Modules.Commerce.Application;

public static class CommerceCheckoutIdempotency
{
    public const int MaxKeyLength = 200;

    public static string? NormalizeKey(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var key = raw.Trim();
        if (key.Length > MaxKeyLength)
        {
            throw new InvalidOperationException("Idempotency-Key must be at most 200 characters.");
        }

        return key;
    }

    public static string Fingerprint(
        Guid tenantId,
        string productSlug,
        string email,
        string? coupon,
        int quantity,
        Guid? sessionId,
        string? interval = null,
        Guid? priceId = null)
    {
        var material = string.Join('\n',
            tenantId.ToString("D"),
            productSlug.Trim().ToLowerInvariant(),
            email.Trim().ToLowerInvariant(),
            (coupon ?? "").Trim().ToUpperInvariant(),
            quantity.ToString(),
            sessionId?.ToString("D") ?? "",
            (interval ?? "").Trim().ToLowerInvariant(),
            priceId?.ToString("D") ?? "");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool IsReplayableOpen(Domain.Aggregates.CheckoutSession session, DateTime utcNow) =>
        session.Status == "OPEN" && session.ExpiresAt > utcNow;

    public static bool TryReplayUrl(Domain.Aggregates.CheckoutSession session, DateTime utcNow, out string? url)
    {
        url = null;
        if (!IsReplayableOpen(session, utcNow) || string.IsNullOrWhiteSpace(session.GatewayCheckoutUrl))
        {
            return false;
        }

        url = session.GatewayCheckoutUrl;
        return true;
    }

    public static bool ShouldReleaseKey(Domain.Aggregates.CheckoutSession session, DateTime utcNow) =>
        !IsReplayableOpen(session, utcNow);
}
