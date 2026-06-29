using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lazuar.ApiTypes;

namespace Modules.Vault.Application.Queries;

public interface IVaultQueryService
{
    Task<IEnumerable<VaultAssetDto>> GetAssetsAsync(Guid organizationId);
    Task<VaultAssetDto?> GetAssetByIdAsync(Guid organizationId, Guid assetId);
}
