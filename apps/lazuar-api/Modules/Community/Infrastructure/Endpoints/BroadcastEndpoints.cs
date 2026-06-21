// apps/lazuar-api/Modules/Community/Infrastructure/Endpoints/BroadcastEndpoints.cs
using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Modules.Community.Application.Commands.Agent;

namespace Modules.Community.Infrastructure;

public static class BroadcastEndpoints
{
    public static RouteGroupBuilder MapBroadcastEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/broadcasts", async Task<Ok<IdResponse>> (
            CreateBroadcastRequestDto req, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            Guid? targetPlanId = !string.IsNullOrEmpty(req.Target_plan_id) ? Guid.Parse(req.Target_plan_id) : null;
            
            var command = new SendBroadcastCommand(
                ctx.TenantId,
                req.Subject,
                req.Body,
                req.Channel,
                targetPlanId,
                req.Target_status,
                req.Target_is_reminder_only);
            
            var id = await mediator.Send(command);
            return TypedResults.Ok(new IdResponse { Id = id.ToString() });
        });

        return group;
    }
}
