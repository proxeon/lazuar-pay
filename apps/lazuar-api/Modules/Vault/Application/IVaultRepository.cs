using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Modules.Vault.Domain.Aggregates;
using Lazuar.ApiTypes;

namespace Modules.Vault.Application;

public interface IVaultRepository
{
    Task<VaultAsset?> GetByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default);
    void Add(VaultAsset asset);
    void Remove(VaultAsset asset);
    Task SaveChangesAsync(CancellationToken ct = default);
    Task<IEnumerable<PortalVaultAssetDto>> GetPortalAssetsAsync(Guid organizationId, IEnumerable<Guid> productIds, CancellationToken ct = default);
}
