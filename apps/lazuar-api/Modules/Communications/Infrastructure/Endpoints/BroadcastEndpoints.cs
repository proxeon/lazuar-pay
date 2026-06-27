using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Modules.Communications.Contracts.Commands;

namespace Modules.Communications.Infrastructure;

public static class BroadcastEndpoints
{
    public static RouteGroupBuilder MapBroadcastEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/broadcasts", async Task<Ok<IdResponse>> (
            CreateBroadcastRequestDto req, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            var command = new SendBroadcastCommand(
                ctx.TenantId,
                req.Subject,
                req.Email_body,
                req.Whatsapp_body,
                req.Channel);
            
            var id = await mediator.Send(command);
            return TypedResults.Ok(new IdResponse { Id = id.ToString() });
        });

        return group;
    }
}
