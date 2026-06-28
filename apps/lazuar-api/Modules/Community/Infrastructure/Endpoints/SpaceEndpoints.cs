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
using Modules.Community.Application.Queries;

namespace Modules.Community.Infrastructure;

public record CreateCommunitySpaceRequest(List<string> Product_ids, string Name, string? Telegram_link, string? Zoom_link);
public record UpdateCommunitySpaceRequest(List<string> Product_ids, string Name, string? Telegram_link, string? Zoom_link);

public static class SpaceEndpoints
{
    public static RouteGroupBuilder MapSpaceEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/spaces", async Task<Ok<ICollection<AdminCommunitySpaceDto>>> (
            IExecutionContextAccessor ctx,
            ICommunityQueryService queryService) =>
        {
            var spaces = await queryService.GetAdminSpacesAsync(ctx.TenantId);
            return TypedResults.Ok((ICollection<AdminCommunitySpaceDto>)spaces.ToList());
        });

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

        group.MapPut("/spaces/{id:guid}", async Task<Ok<StatusResponse>> (
            Guid id,
            UpdateCommunitySpaceRequest req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var productIds = req.Product_ids?.Select(pid => Guid.TryParse(pid, out var parsed) ? parsed : Guid.Empty)
                                            .Where(pid => pid != Guid.Empty)
                                            .ToList() ?? new List<Guid>();

            var updateSpaceCmd = new UpdateCommunitySpaceCommand(
                ctx.TenantId,
                id,
                productIds,
                req.Name,
                req.Telegram_link,
                req.Zoom_link
            );

            await mediator.Send(updateSpaceCmd);

            return TypedResults.Ok(new StatusResponse { Status = "updated" });
        });

        group.MapDelete("/spaces/{id:guid}", async Task<Ok<StatusResponse>> (
            Guid id,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            await mediator.Send(new DeleteCommunitySpaceCommand(ctx.TenantId, id));

            return TypedResults.Ok(new StatusResponse { Status = "deleted" });
        });

        return group;
    }
}
