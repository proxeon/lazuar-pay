using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Modules.Vault.Application;
using Modules.Vault.Domain.Aggregates;
using Lazuar.ApiTypes;

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

    public async Task<IEnumerable<PortalVaultAssetDto>> GetPortalAssetsAsync(Guid organizationId, IEnumerable<Guid> productIds, CancellationToken ct = default)
    {
        var assets = await _context.VaultAssets
            .AsNoTracking()
            .Where(a => a.OrganizationId == organizationId)
            .ToListAsync(ct);

        var filteredAssets = assets.Where(a => a.ProductIds.Any(id => productIds.Contains(id)));

        return filteredAssets.Select(a => new PortalVaultAssetDto
        {
            Product_ids = a.ProductIds.Select(id => id.ToString()).ToList(),
            Name = a.Name,
            Cloudflare_r2_url = a.CloudflareR2Url
        });
    }
}
