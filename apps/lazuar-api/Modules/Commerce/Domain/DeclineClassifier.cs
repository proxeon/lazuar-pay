using System;
using System.Collections.Generic;

namespace Modules.Commerce.Domain;

/// <summary>
/// Static Stripe decline-code table. Hard codes must not create another off-session PaymentIntent.
/// Missing / unknown / NSF / generic <c>charge_declined</c> are soft.
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
        "transaction_not_allowed"
    };

    public static string Classify(string? declineCode) =>
        !string.IsNullOrWhiteSpace(declineCode) && HardCodes.Contains(declineCode.Trim())
            ? Hard
            : Soft;

    public static bool IsHard(string? declineCode) => Classify(declineCode) == Hard;
}
