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
using Modules.Community.Application.Commands;

namespace Modules.Community.Infrastructure;

public record CreateCommunitySpaceRequest(List<string> Product_ids, string Name, string? Telegram_link, string? Zoom_link);

public static class SpaceEndpoints
{
    public static RouteGroupBuilder MapSpaceEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/spaces", async Task<Ok<IdResponse>> (
            CreateCommunitySpaceRequest req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var productIds = req.Product_ids?.Select(id => Guid.TryParse(id, out var parsed) ? parsed : Guid.Empty)
                                            .Where(id => id != Guid.Empty)
                                            .ToList() ?? new List<Guid>();

            var createSpaceCmd = new CreateCommunitySpaceCommand(
                ctx.TenantId,
                productIds,
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
