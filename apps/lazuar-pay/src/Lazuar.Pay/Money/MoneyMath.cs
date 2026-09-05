using Lazuar.Pay.Hosting;

namespace Lazuar.Pay.Money;

public static class MoneyMath
{
    public const decimal MaxQuotedAmount = 99_999_999m;

    public static IResult? QuotedAmountError(decimal? amount)
    {
        if (amount is null || amount <= 0)
        {
            return PayErrors.Status(400, "Bad Request", "amount must be greater than 0");
        }

        if (amount.Value > MaxQuotedAmount)
        {
            return PayErrors.Status(400, "Bad Request", "amount is too large");
        }

        if (ExceedsTwoDecimals(amount.Value))
        {
            return PayErrors.Status(400, "Bad Request", "amount must have at most 2 decimal places");
        }

        return null;
    }

    /// <summary>
    /// True when the value cannot be stored exactly in the ledger's numeric(18,2) — the
    /// database would silently round it. Issue 001 (issues/003): a refund of 0.001 stored
    /// as 0.00 reserved nothing while RefundStripeAsync turned the zero minor amount into
    /// an amount-less ("refund everything") processor call. Every money entry point must
    /// refuse such values instead of letting the column round them.
    /// </summary>
    public static bool ExceedsTwoDecimals(decimal amount) =>
        decimal.Round(amount, 2, MidpointRounding.AwayFromZero) != amount;

    public static long ToMinor(decimal amount) =>
        (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

    public static decimal FromMinor(decimal cents) => cents / 100m;

    public static bool TryNormalizeCurrency(string? raw, out string currency)
    {
        currency = "";
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var n = raw.Trim().ToUpperInvariant();
        if (n.Length != 3)
        {
            return false;
        }

        currency = n;
        return true;
    }
}
