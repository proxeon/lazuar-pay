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
using Modules.Vault.Application.Queries;

namespace Modules.Vault.Infrastructure;

public record CreateVaultAssetRequest(List<string> Product_ids, string Name, string Cloudflare_r2_url);
public record UpdateVaultAssetRequest(List<string> Product_ids, string Name, string Cloudflare_r2_url);

public static class AssetEndpoints
{
    public static RouteGroupBuilder MapAssetEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/assets", async Task<Ok<ICollection<VaultAssetDto>>> (
            IExecutionContextAccessor ctx,
            IVaultQueryService queryService) =>
        {
            var assets = await queryService.GetAssetsAsync(ctx.TenantId);
            return TypedResults.Ok((ICollection<VaultAssetDto>)assets.ToList());
        });

        group.MapGet("/assets/{id:guid}", async Task<Results<Ok<VaultAssetDto>, NotFound>> (
            Guid id,
            IExecutionContextAccessor ctx,
            IVaultQueryService queryService) =>
        {
            var asset = await queryService.GetAssetByIdAsync(ctx.TenantId, id);
            return asset != null ? TypedResults.Ok(asset) : TypedResults.NotFound();
        });

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

        group.MapPut("/assets/{id:guid}", async Task<Ok<StatusResponse>> (
            Guid id,
            UpdateVaultAssetRequest req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var productIds = req.Product_ids?.Select(pid => Guid.TryParse(pid, out var parsed) ? parsed : Guid.Empty)
                                            .Where(pid => pid != Guid.Empty)
                                            .ToList() ?? new List<Guid>();

            var updateAssetCmd = new UpdateVaultAssetCommand(
                ctx.TenantId,
                id,
                productIds,
                req.Name,
                req.Cloudflare_r2_url
            );

            await mediator.Send(updateAssetCmd);

            return TypedResults.Ok(new StatusResponse { Status = "updated" });
        });

        group.MapDelete("/assets/{id:guid}", async Task<Ok<StatusResponse>> (
            Guid id,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            await mediator.Send(new DeleteVaultAssetCommand(ctx.TenantId, id));

            return TypedResults.Ok(new StatusResponse { Status = "deleted" });
        });

        return group;
    }
}
