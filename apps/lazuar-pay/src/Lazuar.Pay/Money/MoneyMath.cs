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

        if (decimal.Round(amount.Value, 2, MidpointRounding.AwayFromZero) != amount.Value)
        {
            return PayErrors.Status(400, "Bad Request", "amount must have at most 2 decimal places");
        }

        return null;
    }

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
