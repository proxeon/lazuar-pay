using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Modules.Community.Application.Commands;

namespace Modules.Community.Infrastructure;

// Local DTO definition to ensure immediate compilation before TypeSpec generator builds out the types
public record CreateCommunitySpaceRequest(string Product_id, string Name, string? Telegram_link, string? Zoom_link);

public static class SpaceEndpoints
{
    public static RouteGroupBuilder MapSpaceEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/spaces", async Task<Ok<IdResponse>> (
            CreateCommunitySpaceRequest req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var createSpaceCmd = new CreateCommunitySpaceCommand(
                ctx.TenantId,
                Guid.Parse(req.Product_id),
                req.Name,
                req.Telegram_link,
                req.Zoom_link
            );

            var spaceId = await mediator.Send(createSpaceCmd);

            return TypedResults.Ok(new IdResponse { Id = spaceId.ToString() });
        });

        return group;
    }
}
