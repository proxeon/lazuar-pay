// apps/lazuar-api/Modules/Payments/Infrastructure/Gateways/GatewayCommon.cs
using System;

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
    /// Placeholder email when the customer address is blank (Billplz/CHIP gateways require one).
    /// </summary>
    public static string ResolveEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? PlaceholderEmail : email;

    /// <summary>
    /// Product line description with optional quantity suffix and default name.
    /// </summary>
    public static string ProductDescription(string? productName, int quantity) =>
        quantity > 1
            ? $"{productName} (x{quantity})"
            : (string.IsNullOrWhiteSpace(productName) ? DefaultProductName : productName);

    /// <summary>
    /// Minor units via banker's rounding (matches CHIP <c>Math.Round(..., 0)</c>).
    /// </summary>
    public static int ToMinorUnitsRounded(decimal amount, int quantity = 1) =>
        (int)Math.Round(amount * quantity * 100m, 0);

    /// <summary>
    /// Minor units via cast-truncate toward zero (matches Billplz/Razorpay <c>(int)(amount * qty * 100)</c>).
    /// </summary>
    public static int ToMinorUnitsTruncating(decimal amount, int quantity = 1) =>
        (int)(amount * quantity * 100m);
}
