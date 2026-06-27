using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Modules.Vault.Domain.Aggregates;
using Lazuar.ApiTypes;

namespace Modules.Vault.Application;

public interface IVaultRepository
{
    void Add(VaultAsset asset);
    Task SaveChangesAsync(CancellationToken ct = default);
    Task<IEnumerable<PortalVaultAssetDto>> GetPortalAssetsAsync(Guid organizationId, IEnumerable<Guid> productIds, CancellationToken ct = default);
}
