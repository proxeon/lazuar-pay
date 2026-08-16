namespace Modules.Payments.Contracts;

/// <summary>
/// Honest collection-mode matrix. Only Stripe and CHIP Collect can vault and charge off-session.
/// Billplz, Razorpay (not demoable), unknown, and blank names are reminder-only.
/// Refund capability is a separate axis: Razorpay can API-refund; Billplz cannot.
/// </summary>
public static class PaymentGatewayCapabilities
{
    public static bool SupportsOffSession(string? gatewayName)
    {
        var g = Normalize(gatewayName);
        return g is "STRIPE" or "CHIP";
    }

    public static bool IsReminderOnlyGateway(string? gatewayName) => !SupportsOffSession(gatewayName);

    public static bool SupportsApiRefund(string? gatewayName)
    {
        var g = Normalize(gatewayName);
        return g is "STRIPE" or "CHIP" or "RAZORPAY" or "XENDIT";
    }

    /// <summary>Hosted Xendit invoice / CHIP collect may show DuitNow QR. We do not render QR ourselves.</summary>
    public static bool SupportsDuitNowQr(string? gatewayName)
    {
        var g = Normalize(gatewayName);
        return g is "XENDIT" or "CHIP" or "BILLPLZ";
    }

    /// <summary>Wallets appear on the processor hosted page when the merchant enables them there.</summary>
    public static bool SupportsHostedWallet(string? gatewayName, string? wallet)
    {
        var g = Normalize(gatewayName);
        if (g is not ("XENDIT" or "CHIP"))
        {
            return false;
        }

        var w = Normalize(wallet);
        return w is "GRABPAY" or "SHOPEEPAY" or "TNG" or "TOUCHNGO" or "BOOST" or "DUITNOW";
    }

    /// <summary>True FPX auto-debit. Off until Curlec/Xendit mandate tokens soak.</summary>
    public static bool SupportsEmandate(string? gatewayName)
    {
        _ = gatewayName;
        return false;
    }

    public static bool RequiresMarkRefunded(string? gatewayName)
    {
        var g = Normalize(gatewayName);
        return g is "" or "BILLPLZ" or "OFFLINE" or "BANK_TRANSFER" or "CASH" or "MANUAL_OFFLINE" or "COMPED";
    }

    private static string Normalize(string? gatewayName) => (gatewayName ?? "").Trim().ToUpperInvariant();
}
