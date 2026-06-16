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

public static class Endpoints
{
    public static IEndpointRouteBuilder MapLhdnEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/lhdn").RequireAuthorization("OrgAdmin");

        group.MapPost("/documents", async Task<Ok<StatusResponse>> (
            [FromBody] SubmitDocumentRequestDto req, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            await mediator.Send(new SubmitTaxDocumentCommand(ctx.TenantId, req));
            return TypedResults.Ok(new StatusResponse { Status = "accepted_for_processing" });
        });

        group.MapGet("/documents/{internalId}", async Task<Results<Ok<LhdnDocumentResponseDto>, NotFound>> (
            string internalId, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            var result = await mediator.Send(new GetLhdnDocumentStatusQuery(ctx.TenantId, internalId));
            return result != null ? TypedResults.Ok(result) : TypedResults.NotFound();
        });

        group.MapPost("/webhooks", async Task<Ok<IdResponse>> (
            [FromBody] RegisterWebhookRequestDto req, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            var id = await mediator.Send(new RegisterWebhookCommand(ctx.TenantId, req));
            return TypedResults.Ok(new IdResponse { Id = id.ToString() });
        });

        group.MapGet("/webhooks", async Task<Ok<ICollection<WebhookSubscriptionDto>>> (
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            var result = await mediator.Send(new ListWebhooksQuery(ctx.TenantId));
            return TypedResults.Ok((ICollection<WebhookSubscriptionDto>)result);
        });

        group.MapDelete("/webhooks/{id:guid}", async Task<Ok<StatusResponse>> (
            Guid id, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            await mediator.Send(new DeleteWebhookCommand(ctx.TenantId, id));
            return TypedResults.Ok(new StatusResponse { Status = "deleted" });
        });

        group.MapPut("/workspaces/{id:guid}/lhdn-certificate", async Task<Results<Ok<StatusResponse>, UnauthorizedHttpResult>> (
            Guid id, 
            [FromBody] UpdateLhdnCertificateRequestDto req, 
            IExecutionContextAccessor ctx, 
            IMediator mediator) =>
        {
            if (ctx.TenantId != id && !ctx.IsSystemAdmin) return TypedResults.Unauthorized();
            
            await mediator.Send(new UpdateLhdnCertificateCommand(id, req.P12_base64_file, req.Passphrase));
            return TypedResults.Ok(new StatusResponse { Status = "updated" });
        });

        return endpoints;
    }
}
