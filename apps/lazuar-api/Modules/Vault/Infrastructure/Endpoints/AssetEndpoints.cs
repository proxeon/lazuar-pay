using System;
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

// Local DTO definition to ensure immediate compilation before TypeSpec generator builds out the types
public record CreateVaultAssetRequest(string Product_id, string Name, string Cloudflare_r2_url);

public static class AssetEndpoints
{
    public static RouteGroupBuilder MapAssetEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/assets", async Task<Ok<IdResponse>> (
            CreateVaultAssetRequest req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var createAssetCmd = new CreateVaultAssetCommand(
                ctx.TenantId,
                Guid.Parse(req.Product_id),
                req.Name,
                req.Cloudflare_r2_url
            );

            var assetId = await mediator.Send(createAssetCmd);

            return TypedResults.Ok(new IdResponse { Id = assetId.ToString() });
        });

        return group;
    }
}
