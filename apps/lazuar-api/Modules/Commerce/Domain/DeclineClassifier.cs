using System;
using System.Collections.Generic;

namespace Modules.Commerce.Domain;

/// <summary>
/// Decline-code table. Hard codes must not create another off-session PaymentIntent.
/// Stripe <c>expired_card</c> cannot succeed until the buyer updates the card (B03-C20).
/// Missing / unknown / NSF / generic <c>charge_declined</c> / CHIP-shaped codes stay soft.
/// </summary>
public static class DeclineClassifier
{
    public const string Hard = "hard";
    public const string Soft = "soft";

    private static readonly HashSet<string> HardCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "incorrect_number",
        "lost_card",
        "pickup_card",
        "stolen_card",
        "revocation_of_authorization",
        "revocation_of_all_authorizations",
        "authentication_required",
        "highest_risk_level",
        "transaction_not_allowed",
        "expired_card",
        "invalid_expiry_month",
        "invalid_expiry_year"
    };

    public static string Classify(string? declineCode) =>
        !string.IsNullOrWhiteSpace(declineCode) && HardCodes.Contains(declineCode.Trim())
            ? Hard
            : Soft;

    public static bool IsHard(string? declineCode) => Classify(declineCode) == Hard;
}
