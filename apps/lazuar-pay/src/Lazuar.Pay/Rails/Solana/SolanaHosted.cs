using Lazuar.Pay.Data;
using Microsoft.EntityFrameworkCore;

namespace Lazuar.Pay.Rails.Solana;

public sealed class SolanaHosted(PayDbContext db) : IHostedRail
{
    public string Provider => PayProviders.Solana;

    public async Task<HostedSession> CreateHostedUrlAsync(CheckoutRow checkout, CancellationToken ct)
    {
        var cred = await db.GatewayCredentials.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrgId == checkout.OrgId && x.Provider == PayProviders.Solana, ct);
        if (cred is null || string.IsNullOrWhiteSpace(cred.PublicMerchantId))
        {
            throw new InvalidOperationException("rail not configured");
        }

        return SolanaPayUri.Create(checkout, cred);
    }
}
