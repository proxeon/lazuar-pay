// apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/GatewayCommon.cs
using System;
using System.Collections.Generic;

namespace Modules.Payments.Infrastructure.Gateways;

/// <summary>
/// Shared pure helpers for payment gateway adapters (name/email defaults, minor units, descriptions).
/// Adapters call these statically — no abstract base class, no shared HTTP.
/// </summary>
internal static class GatewayCommon
{
    public const string DefaultProductName = "Lazuar Payment";
    public const string PlaceholderEmail = "customer@example.com";

    /// <summary>
    /// Local-part of an email, or <c>"Customer"</c> when missing/malformed.
    /// </summary>
    public static string ExtractName(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "Customer";
        var atIndex = email.IndexOf('@');
        return atIndex > 0 ? email[..atIndex] : "Customer";
    }

    /// <summary>
    /// Never send this to a processor as a real buyer. Quotes already refuse it (158/192).
    /// </summary>
    public static bool IsUsableBuyerEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email)
        && !string.Equals(email.Trim(), PlaceholderEmail, StringComparison.OrdinalIgnoreCase);

    public static bool TryResolveEmail(string? email, out string resolved, out string? error)
    {
        if (!IsUsableBuyerEmail(email))
        {
            resolved = "";
            error = "Customer email is required.";
            return false;
        }

        resolved = email!.Trim();
        error = null;
        return true;
    }

    /// <summary>
    /// Fail-closed: blank / <see cref="PlaceholderEmail"/> throws instead of inventing a processor customer.
    /// </summary>
    public static string ResolveEmail(string? email)
    {
        if (!TryResolveEmail(email, out var resolved, out var error))
        {
            throw new InvalidOperationException(error);
        }

        return resolved;
    }

    /// <summary>
    /// Product line description with optional quantity suffix and default name.
    /// </summary>
    public static string ProductDescription(string? productName, int quantity) =>
        quantity > 1
            ? $"{productName} (x{quantity})"
            : (string.IsNullOrWhiteSpace(productName) ? DefaultProductName : productName);

    private static readonly HashSet<string> ZeroDecimalCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "BIF", "CLP", "DJF", "GNF", "ISK", "JPY", "KMF", "KRW", "PYG",
        "RWF", "UGX", "VND", "VUV", "XAF", "XOF", "XPF"
    };

    public static bool IsZeroDecimalCurrency(string? currency) =>
        !string.IsNullOrWhiteSpace(currency) && ZeroDecimalCurrencies.Contains(currency.Trim());

    /// <summary>
    /// One money policy: half away from zero. Zero-decimal ISO currencies are not ×100.
    /// </summary>
    public static long ToMinorUnits(decimal amount, string? currency = "MYR", int quantity = 1)
    {
        var qty = quantity < 1 ? 1 : quantity;
        var factor = IsZeroDecimalCurrency(currency) ? 1m : 100m;
        return (long)Math.Round(amount * qty * factor, 0, MidpointRounding.AwayFromZero);
    }

    public static int ToMinorUnitsRounded(decimal amount, int quantity = 1) =>
        (int)ToMinorUnits(amount, "MYR", quantity);

    public static int ToMinorUnitsTruncating(decimal amount, int quantity = 1) =>
        (int)ToMinorUnits(amount, "MYR", quantity);

    /// <summary>
    /// Keep an existing paying <c>tenant_id</c> (platform charges). Stamp the adapter
    /// tenant as <c>platform_tenant_id</c> when it differs so system checkout does not
    /// overwrite the workspace that must be activated.
    /// </summary>
    public static bool TryNormalizeCurrency(string? raw, out string currency)
    {
        currency = "";
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalized = raw.Trim().ToUpperInvariant();
        if (normalized.Length != 3)
        {
            return false;
        }

        currency = normalized;
        return true;
    }

    /// <summary>
    /// Set when we know (or do not know) the processor MDR. A zero
    /// <c>GatewayFee</c> with status <c>unknown</c> is not "the fee is zero".
    /// </summary>
    public const string GatewayFeeStatusKey = "gateway_fee_status";
    public const string GatewayFeeStatusKnown = "known";
    public const string GatewayFeeStatusUnknown = "unknown";

    public static void StampGatewayFeeStatus(Dictionary<string, string> meta, bool feeKnown) =>
        meta[GatewayFeeStatusKey] = feeKnown ? GatewayFeeStatusKnown : GatewayFeeStatusUnknown;

    internal const string RefundIdempotencyKeyPrefix = "lazuar-refund:";

    public static string FormatRefundIdempotencyKey(string transactionId, decimal amount)
    {
        return RefundIdempotencyKeyPrefix + transactionId + ":" + ToMinorUnits(amount);
    }

    public static void ApplyPayingTenantMetadata(Dictionary<string, string> metadata, Guid adapterTenantId)
    {
        var adapterId = adapterTenantId.ToString();
        if (!metadata.TryGetValue("tenant_id", out var existing) || string.IsNullOrWhiteSpace(existing))
        {
            metadata["tenant_id"] = adapterId;
            return;
        }

        if (!string.Equals(existing, adapterId, StringComparison.OrdinalIgnoreCase))
            metadata["platform_tenant_id"] = adapterId;
    }
}
