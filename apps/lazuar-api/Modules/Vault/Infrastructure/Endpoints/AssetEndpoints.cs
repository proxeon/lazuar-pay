using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Modules.Vault.Application.Commands;

namespace Modules.Vault.Infrastructure;

public record CreateVaultAssetRequest(List<string> Product_ids, string Name, string Cloudflare_r2_url);

public static class AssetEndpoints
{
    public static RouteGroupBuilder MapAssetEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/assets", async Task<Ok<IdResponse>> (
            CreateVaultAssetRequest req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var productIds = req.Product_ids?.Select(id => Guid.TryParse(id, out var parsed) ? parsed : Guid.Empty)
                                            .Where(id => id != Guid.Empty)
                                            .ToList() ?? new List<Guid>();

            var createAssetCmd = new CreateVaultAssetCommand(
                ctx.TenantId,
                productIds,
                req.Name,
                req.Cloudflare_r2_url
            );

            var assetId = await mediator.Send(createAssetCmd);

            return TypedResults.Ok(new IdResponse { Id = assetId.ToString() });
        });

        return group;
    }
}
