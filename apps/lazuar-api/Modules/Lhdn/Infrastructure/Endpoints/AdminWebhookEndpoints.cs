using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modules.Lhdn.Application.Commands;
using Modules.Lhdn.Application.Queries;

namespace Modules.Lhdn.Infrastructure;

public static class AdminWebhookEndpoints
{
    public static IEndpointRouteBuilder MapLhdnAdminWebhookEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/lhdn").RequireAuthorization("OrgAdmin");

        admin.MapPost("/webhooks", async Task<Ok<IdResponse>> (
            [FromBody] RegisterWebhookRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var id = await mediator.Send(new RegisterWebhookCommand(ctx.TenantId, req));
            return TypedResults.Ok(new IdResponse { Id = id.ToString() });
        });

        admin.MapGet("/webhooks", async Task<Ok<ICollection<WebhookSubscriptionDto>>> (
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var result = await mediator.Send(new ListWebhooksQuery(ctx.TenantId));
            return TypedResults.Ok((ICollection<WebhookSubscriptionDto>)result);
        });

        admin.MapDelete("/webhooks/{id:guid}", async Task<Ok<StatusResponse>> (
            Guid id,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            await mediator.Send(new DeleteWebhookCommand(ctx.TenantId, id));
            return TypedResults.Ok(new StatusResponse { Status = "deleted" });
        });

        return endpoints;
    }
}
