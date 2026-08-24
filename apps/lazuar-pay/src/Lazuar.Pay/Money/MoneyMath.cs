namespace Lazuar.Pay.Money;

public static class MoneyMath
{
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
