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
using Modules.Community.Application.Commands;

namespace Modules.Community.Infrastructure;

// Local DTO definition to ensure immediate compilation before TypeSpec generator builds out the types
public record CreateCommunitySpaceRequest(string Name, string Slug, decimal Price, string Interval, string? Telegram_link, string? Zoom_link);

public static class SpaceEndpoints
{
    public static RouteGroupBuilder MapSpaceEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/spaces", async Task<Ok<IdResponse>> (
            CreateCommunitySpaceRequest req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            // Distributed Orchestration: Guarantee that the Commerce.Product and Community.Space are saved 
            // successfully in tandem. Rollback entire operation if any validation or insertion fails to prevent dangling data.
            using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

            var createProductCmd = new CreateProductCommand(
                ctx.TenantId,
                req.Name,
                req.Slug,
                req.Price,
                "MYR",
                req.Interval,
                RequiresAddress: false, 
                RequiresTaxId: false, 
                RequiresPhone: true, // Phone typically required for community WhatsApp delivery
                new List<string> { "internal:community" }
            );

            var productId = await mediator.Send(createProductCmd);

            var createSpaceCmd = new CreateCommunitySpaceCommand(
                ctx.TenantId,
                productId,
                req.Name,
                req.Telegram_link,
                req.Zoom_link
            );

            var spaceId = await mediator.Send(createSpaceCmd);

            scope.Complete();

            return TypedResults.Ok(new IdResponse { Id = spaceId.ToString() });
        });

        return group;
    }
}
