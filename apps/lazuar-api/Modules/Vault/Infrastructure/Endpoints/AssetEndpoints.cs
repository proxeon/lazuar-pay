using System.Collections.Generic;
using System.Threading.Tasks;
using System.Transactions;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Modules.Commerce.Contracts.Commands;
using Modules.Vault.Application.Commands;

namespace Modules.Vault.Infrastructure;

// Local DTO definition to ensure immediate compilation before TypeSpec generator builds out the types
public record CreateVaultAssetRequest(string Name, string Slug, decimal Price, string Cloudflare_r2_url);

public static class AssetEndpoints
{
    public static RouteGroupBuilder MapAssetEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/assets", async Task<Ok<IdResponse>> (
            CreateVaultAssetRequest req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            // Distributed Orchestration: Guarantee that the Commerce.Product and Vault.Asset are saved 
            // successfully in tandem. Rollback entire operation if any validation or insertion fails to prevent dangling files.
            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

            var createProductCmd = new CreateProductCommand(
                ctx.TenantId,
                req.Name,
                req.Slug,
                req.Price,
                "MYR",
                "one_time",
                RequiresAddress: false, 
                RequiresTaxId: false, 
                RequiresPhone: false, // Digital downloads usually only require an email
                new List<string> { "internal:vault" }
            );

            var productId = await mediator.Send(createProductCmd);

            var createAssetCmd = new CreateVaultAssetCommand(
                ctx.TenantId,
                productId,
                req.Name,
                req.Cloudflare_r2_url
            );

            var assetId = await mediator.Send(createAssetCmd);

            scope.Complete();

            return TypedResults.Ok(new IdResponse { Id = assetId.ToString() });
        });

        return group;
    }
}
