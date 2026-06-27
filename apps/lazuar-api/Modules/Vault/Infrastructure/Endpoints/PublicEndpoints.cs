using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modules.One.Contracts;
using Modules.Vault.Application;

namespace Modules.Vault.Infrastructure;

public static class PublicEndpoints
{
    public static RouteGroupBuilder MapPublicVaultEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/{tenantSlug}/portal/assets", async Task<Results<Ok<ICollection<PortalVaultAssetDto>>, NotFound>> (
            string tenantSlug,
            [FromQuery] string[] product_ids,
            IOneQueryService oneQueryService,
            IVaultRepository repository) =>
        {
            var tenantId = await oneQueryService.GetTenantIdBySlugAsync(tenantSlug);
            if (!tenantId.HasValue) return TypedResults.NotFound();

            var parsedIds = product_ids.Select(id => Guid.TryParse(id, out var parsed) ? parsed : Guid.Empty).Where(id => id != Guid.Empty).ToList();
            
            var assets = await repository.GetPortalAssetsAsync(tenantId.Value, parsedIds);
            
            return TypedResults.Ok((ICollection<PortalVaultAssetDto>)assets.ToList());
        });

        return group;
    }
}
