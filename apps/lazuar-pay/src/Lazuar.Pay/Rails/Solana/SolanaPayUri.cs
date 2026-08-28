using System.Globalization;
using System.Security.Cryptography;
using Lazuar.Pay.Data;

namespace Lazuar.Pay.Rails.Solana;

public static class SolanaPayUri
{
    public static HostedSession Create(CheckoutRow checkout, GatewayCredentialRow cred)
    {
        if (!SolanaAddress.TryNormalize(cred.PublicMerchantId, out var recipient))
        {
            throw new InvalidOperationException("rail not configured");
        }

        if (!SolanaMoney.TryToAtomic(checkout.Amount, out _))
        {
            throw new InvalidOperationException("amount is not a valid USDC amount");
        }

        var mint = SolanaUsdc.MintFor(cred.Environment);
        var reference = SolanaBase58.Encode(RandomNumberGenerator.GetBytes(32));
        var amount = checkout.Amount.ToString("0.######", CultureInfo.InvariantCulture);
        var memo = Uri.EscapeDataString(checkout.Id);
        var label = Uri.EscapeDataString("Lazuar Pay");
        var url = "solana:" + recipient
            + "?amount=" + amount
            + "&spl-token=" + mint
            + "&reference=" + reference
            + "&label=" + label
            + "&memo=" + memo;
        return new HostedSession("", reference, url);
    }
}
