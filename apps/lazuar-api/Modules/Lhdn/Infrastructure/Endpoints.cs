using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
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
        // SDK / integration surface: human admins + machine API_CLIENT principals.
        var documents = endpoints.MapGroup("/lhdn").RequireAuthorization("IntegrationLhdnDocuments");

        documents.MapPost("/documents", async Task<Results<Ok<StatusResponse>, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>>> (
            [FromBody] SubmitDocumentRequestDto req,
            HttpRequest httpRequest,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var idempotencyKey = httpRequest.Headers["Idempotency-Key"].ToString();
            if (string.IsNullOrWhiteSpace(idempotencyKey))
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = "Idempotency-Key header is required for SDK submissions." });
            }

            try
            {
                await mediator.Send(new SubmitTaxDocumentCommand(ctx.TenantId, idempotencyKey, req));
                return TypedResults.Ok(new StatusResponse { Status = "accepted_for_processing" });
            }
            catch (BusinessRuleValidationException ex)
            {
                // Handles 402 Insufficient Credits and XML Schema failures cleanly
                var status = ex.Message.StartsWith("402") ? 402 : 400;
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = status, Detail = ex.Message });
            }
        });

        documents.MapGet("/documents/{internalId}", async Task<Results<Ok<LhdnDocumentResponseDto>, NotFound>> (
            string internalId,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var result = await mediator.Send(new GetLhdnDocumentStatusQuery(ctx.TenantId, internalId));
            return result != null ? TypedResults.Ok(result) : TypedResults.NotFound();
        });

        documents.MapPost("/documents/{internalId}/cancel", async Task<Results<Ok<StatusResponse>, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>>> (
            string internalId,
            [FromBody] CancelDocumentRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            try
            {
                await mediator.Send(new CancelTaxDocumentCommand(ctx.TenantId, internalId, req.Reason));
                return TypedResults.Ok(new StatusResponse { Status = "cancelled" });
            }
            catch (BusinessRuleValidationException ex)
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = ex.Message });
            }
        });

        // Dashboard / org-admin surface: no API_CLIENT (key mint, webhooks, certificate).
        var admin = endpoints.MapGroup("/lhdn").RequireAuthorization("OrgAdmin");

        admin.MapPost("/api-keys", async Task<Ok<GenerateApiKeyResponseDto>> (
            [FromBody] GenerateApiKeyRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var rawKey = await mediator.Send(new GenerateApiKeyCommand(ctx.TenantId, req.Name, req.Is_test_mode));
            return TypedResults.Ok(new GenerateApiKeyResponseDto { Plain_key = rawKey });
        });

        admin.MapDelete("/api-keys/{id:guid}", async Task<Results<Ok<StatusResponse>, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>>> (
            Guid id,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            try
            {
                await mediator.Send(new RevokeApiKeyCommand(ctx.TenantId, id));
                return TypedResults.Ok(new StatusResponse { Status = "revoked" });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = ex.Message });
            }
        });

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

        admin.MapPut("/workspaces/{id:guid}/lhdn-certificate", async Task<Results<Ok<StatusResponse>, UnauthorizedHttpResult>> (
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
