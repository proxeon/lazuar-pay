using Lazuar.Pay.Rails;
using Lazuar.Pay.Rails.Solana;

namespace Lazuar.Pay.Money;

/// <summary>
/// Issues 003 and 014 (issues/001): which currencies each rail may settle.
/// <see cref="MoneyMath.ToMinor"/> multiplies by 100 — it assumes two-decimal currencies —
/// and every rail conversion hardcodes that assumption. A zero-decimal currency (JPY, KRW,
/// VND, …) therefore used to produce a processor charge 100× the quoted amount while the
/// ledger booked the quote, invisible to the webhook amount check (both sides ran through
/// the same ×100). Those codes are rejected until exponent-aware conversion exists.
/// Rails also only settle what they actually bill: Billplz and CHIP bill MYR only and
/// Razorpay settles INR — a USD quote on those rails used to collect MYR at the processor
/// while charge, journal, and receipt booked USD.
/// </summary>
public static class RailCurrencies
{
    /// <summary>
    /// Every currency the system may quote. All are two-decimal ISO-4217 codes — the set
    /// <see cref="MoneyMath.ToMinor"/> is correct for. Zero-decimal codes must NOT be added
    /// here without making ToMinor/FromMinor exponent-aware first.
    /// </summary>
    public static readonly string[] TwoDecimal =
        ["MYR", "USD", "SGD", "EUR", "GBP", "AUD", "NZD", "CHF", "CAD", "HKD", "THB", "PHP", "IDR", "INR", "CNY", "TWD"];

    static readonly IReadOnlyDictionary<string, string[]> ByProvider =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            // Stripe settles two-decimal currencies; IDR is two-decimal on Stripe.
            [PayProviders.Stripe] = TwoDecimal,
            // Xendit's regional coverage.
            [PayProviders.Xendit] = ["IDR", "MYR", "PHP", "THB", "SGD"],
            // Billplz bills are MYR-only — the payload never carries a currency.
            [PayProviders.Billplz] = ["MYR"],
            // CHIP purchases are MYR.
            [PayProviders.Chip] = ["MYR"],
            // Razorpay settles INR.
            [PayProviders.Razorpay] = ["INR"],
            // The no-op test rail accepts what the suite exercises.
            [PayProviders.Test] = ["MYR", "USD"],
        };

    public static bool IsSupported(string provider, string currency)
    {
        if (PayProviders.IsSolana(provider))
        {
            // Solana's USDC rules (amounts, decimals) are validated by SolanaMoney.MintError.
            return string.Equals(currency, SolanaUsdc.Currency, StringComparison.OrdinalIgnoreCase);
        }

        return ByProvider.TryGetValue(provider, out var list)
            && list.Contains(currency, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Human-readable list for the 400 message.</summary>
    public static string Describe(string provider) =>
        PayProviders.IsSolana(provider)
            ? SolanaUsdc.Currency
            : string.Join(", ", ByProvider.TryGetValue(provider, out var list) ? list : []);

    /// <summary>
    /// The currency a NEW checkout or pay link quotes by default on this rail — the catalog
    /// currency (MYR) wherever the rail settles it, else the rail's own. Issues 003 and 014
    /// (issues/001) made this table binding server-side; issue 003 (issues/003) broke the
    /// merchant dashboard because its local mirror said MYR for razorpay, which this rail
    /// only ever rejects. Exposed on /gateways processor payloads so the UI reads the
    /// server's answer instead of trusting its mirror.
    /// </summary>
    public static string Default(string provider) =>
        PayProviders.IsSolana(provider) ? SolanaUsdc.Currency
        : provider == PayProviders.Razorpay ? "INR"
        : "MYR";
}
