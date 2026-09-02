using Lazuar.Pay.Hosting;
using Lazuar.Pay.Rails;

namespace Lazuar.Pay.Rails.Solana;

public static class SolanaMoney
{
    public static IResult? MintError(string provider, string? currency, string? interval, string? productId, decimal? amount)
    {
        if (!PayProviders.IsSolana(provider))
        {
            return null;
        }

        if (interval is "mo" or "yr")
        {
            return PayErrors.Status(400, "Bad Request", "solana does not support subscriptions");
        }

        if (!PayProviders.UsesCatalogProduct(provider) && !string.IsNullOrWhiteSpace(productId))
        {
            return PayErrors.Status(400, "Bad Request", "solana does not use a MYR catalog product");
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            return PayErrors.Status(400, "Bad Request", "solana currency must be USDC");
        }

        var n = currency.Trim().ToUpperInvariant();
        if (n == "MYR")
        {
            return PayErrors.Status(400, "Bad Request", "solana does not capture ringgit");
        }

        if (n == "USD")
        {
            return PayErrors.Status(400, "Bad Request", "solana receives USDC, not USD");
        }

        if (n != SolanaUsdc.Currency)
        {
            return PayErrors.Status(400, "Bad Request", "solana currency must be USDC");
        }

        if (amount is decimal value)
        {
            if (!TryToAtomic(value, out _))
            {
                return PayErrors.Status(400, "Bad Request", "amount is not a valid USDC amount");
            }

            // Amounts are stored numeric(18,2) and the QR is validated against the stored
            // value. Accepting sub-cent mints would render a QR whose exact payment can never
            // confirm — the buyer's USDC lands on the receive address with no booking and no
            // refund path. Cent-quoted only.
            if (decimal.Round(value, 2, MidpointRounding.AwayFromZero) != value)
            {
                return PayErrors.Status(400, "Bad Request", "solana amounts support at most 2 decimal places");
            }
        }

        return null;
    }

    public static bool TryToAtomic(decimal amount, out long atomic)
    {
        atomic = 0;
        if (amount <= 0)
        {
            return false;
        }

        var rounded = decimal.Round(amount, SolanaUsdc.Decimals, MidpointRounding.AwayFromZero);
        if (rounded != amount)
        {
            return false;
        }

        var scaled = decimal.Round(amount * 1_000_000m, 0, MidpointRounding.AwayFromZero);
        if (scaled != Math.Floor(scaled) || scaled > long.MaxValue)
        {
            return false;
        }

        atomic = (long)scaled;
        return true;
    }
}
