namespace Lazuar.Pay.Rails;

public static class PayProviders
{
    public const string Stripe = "stripe";
    public const string Chip = "chip";
    public const string Billplz = "billplz";
    public const string Xendit = "xendit";
    public const string Razorpay = "razorpay";

    public const string Capability = "hosted_link";

    public static readonly string[] All = [Stripe, Chip, Billplz, Xendit, Razorpay];

    public static bool TryNormalize(string? raw, out string provider)
    {
        provider = (raw ?? "").Trim().ToLowerInvariant();
        return provider is Stripe or Chip or Billplz or Xendit or Razorpay;
    }

    public static bool RequiresPublicMerchantId(string provider) =>
        provider is Chip or Billplz;

    public static bool RequiresEmail(string provider) =>
        provider is not Stripe;

    public static bool AllowsPublicMerchantId(string provider) =>
        RequiresPublicMerchantId(provider);
}
