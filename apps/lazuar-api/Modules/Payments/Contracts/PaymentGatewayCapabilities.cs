namespace Modules.Payments.Contracts;

/// <summary>
/// Honest collection-mode matrix. Only Stripe and CHIP Collect can vault and charge off-session.
/// Billplz, Razorpay (not demoable), unknown, and blank names are reminder-only.
/// </summary>
public static class PaymentGatewayCapabilities
{
    public static bool SupportsOffSession(string? gatewayName)
    {
        var g = (gatewayName ?? "").Trim().ToUpperInvariant();
        return g is "STRIPE" or "CHIP";
    }

    public static bool IsReminderOnlyGateway(string? gatewayName) => !SupportsOffSession(gatewayName);
}
