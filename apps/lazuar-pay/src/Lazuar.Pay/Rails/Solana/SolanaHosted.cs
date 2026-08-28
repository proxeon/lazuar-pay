using Lazuar.Pay.Data;
using Microsoft.EntityFrameworkCore;

namespace Lazuar.Pay.Rails.Solana;

public sealed class SolanaHosted(PayDbContext db, IConfiguration config) : IHostedRail
{
    public string Provider => PayProviders.Solana;

    public async Task<HostedSession> CreateHostedUrlAsync(CheckoutRow checkout, CancellationToken ct)
    {
        if (SolanaCluster.RpcUrl(config) is null)
        {
            throw new InvalidOperationException("Pay:Solana:RpcUrl is not configured");
        }

        var cred = await db.GatewayCredentials.AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrgId == checkout.OrgId && x.Provider == PayProviders.Solana, ct);
        if (cred is null || string.IsNullOrWhiteSpace(cred.PublicMerchantId))
        {
            throw new InvalidOperationException("rail not configured");
        }

        var cluster = SolanaCluster.FromConfig(config);
        if (!SolanaCluster.MatchesVault(cluster, cred.Environment))
        {
            throw new InvalidOperationException("solana cluster mismatch");
        }

        return SolanaPayUri.Create(checkout, cred, cluster);
    }
}
