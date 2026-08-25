using Microsoft.Extensions.Hosting;

namespace Lazuar.Pay.Rails;

public static class PayProviders
{
    public const string Stripe = "stripe";
    public const string Chip = "chip";
    public const string Billplz = "billplz";
    public const string Xendit = "xendit";
    public const string Razorpay = "razorpay";
    public const string Test = "test";

    public const string Capability = "hosted_link";

    public static readonly string[] All = [Stripe, Chip, Billplz, Xendit, Razorpay];

    public static IReadOnlyList<string> Listed(IHostEnvironment env) =>
        AllowsTest(env) ? [..All, Test] : All;

    public static bool AllowsTest(IHostEnvironment env) =>
        !env.IsProduction();

    public static bool IsTest(string provider) => provider == Test;

    public static bool TryNormalize(string? raw, out string provider)
    {
        provider = (raw ?? "").Trim().ToLowerInvariant();
        return provider is Stripe or Chip or Billplz or Xendit or Razorpay or Test;
    }

    public static bool RequiresPublicMerchantId(string provider) =>
        provider is Chip or Billplz;

    public static bool RequiresEmail(string provider) =>
        provider is not Stripe and not Test;

    public static bool AllowsPublicMerchantId(string provider) =>
        RequiresPublicMerchantId(provider);
}
