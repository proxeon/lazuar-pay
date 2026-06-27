using System.Threading;
using System.Threading.Tasks;
using Modules.Vault.Application;
using Modules.Vault.Domain.Aggregates;

namespace Modules.Vault.Infrastructure.Repositories;

public class VaultRepository : IVaultRepository
{
    private readonly VaultDbContext _context;

    public VaultRepository(VaultDbContext context)
    {
        _context = context;
    }

    public void Add(VaultAsset asset) => _context.VaultAssets.Add(asset);

    public async Task SaveChangesAsync(CancellationToken ct = default) => await _context.SaveChangesAsync(ct);
}
