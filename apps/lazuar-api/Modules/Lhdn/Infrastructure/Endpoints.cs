using System;
using System.Collections.Generic;
using System.Linq;
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
using Modules.Lhdn.Domain;
using Modules.One.Contracts;

namespace Modules.Lhdn.Infrastructure;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapLhdnEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Document write surface: submit + cancel.
        var documentsWrite = endpoints.MapGroup("/lhdn").RequireAuthorization("IntegrationLhdnDocumentsWrite");

        documentsWrite.MapPost("/documents", async Task<Results<Ok<StatusResponse>, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>>> (
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

        documentsWrite.MapPost("/documents/{internalId}/cancel", async Task<Results<Ok<StatusResponse>, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>>> (
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

        // Document read surface: status GET + taxpayer validate (write scope also satisfies read policy).
        var documentsRead = endpoints.MapGroup("/lhdn").RequireAuthorization("IntegrationLhdnDocumentsRead");

        documentsRead.MapGet("/documents/{internalId}", async Task<Results<Ok<LhdnDocumentResponseDto>, NotFound>> (
            string internalId,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            var result = await mediator.Send(new GetLhdnDocumentStatusQuery(ctx.TenantId, internalId));
            return result != null ? TypedResults.Ok(result) : TypedResults.NotFound();
        });

        documentsRead.MapPost("/taxpayer/validate", async Task<Results<Ok<ValidateTinResponseDto>, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>>> (
            [FromBody] ValidateTinRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            try
            {
                var result = await mediator.Send(new ValidateTaxpayerTinCommand(
                    ctx.TenantId,
                    req.Tin,
                    req.Id_type.ToString(),
                    req.Id_value));
                return TypedResults.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = ex.Message });
            }
        });

        // Dashboard / org-admin surface: no API_CLIENT (key mint, webhooks, certificate, tenant config).
        var admin = endpoints.MapGroup("/lhdn").RequireAuthorization("OrgAdmin");

        // API keys are platform-owned (One). Lhdn routes remain as a product façade.
        admin.MapGet("/api-keys", async Task<Ok<ICollection<ApiKeyDto>>> (
            IExecutionContextAccessor ctx,
            [FromServices] IApiCredentialService credentials) =>
        {
            var keys = await credentials.ListAsync(ctx.TenantId);
            var dtos = keys.Select(k => new ApiKeyDto
            {
                Id = k.Id.ToString(),
                Name = k.Name,
                Prefix = k.Prefix,
                Hint = k.Hint,
                Is_active = k.IsActive,
                Created_at = new DateTimeOffset(k.CreatedAt, TimeSpan.Zero),
                Scopes = ApiKeyScopes.Split(k.Scopes).ToList()
            }).ToList();
            return TypedResults.Ok((ICollection<ApiKeyDto>)dtos);
        });

        admin.MapPost("/api-keys", async Task<Ok<GenerateApiKeyResponseDto>> (
            [FromBody] GenerateApiKeyRequestDto req,
            IExecutionContextAccessor ctx,
            [FromServices] IApiCredentialService credentials) =>
        {
            var createdBy = ctx.UserId == Guid.Empty ? (Guid?)null : ctx.UserId;
            var created = await credentials.GenerateAsync(ctx.TenantId, req.Name, req.Is_test_mode, createdBy);
            return TypedResults.Ok(new GenerateApiKeyResponseDto
            {
                Id = created.Id.ToString(),
                Name = created.Name,
                Prefix = created.Prefix,
                Hint = created.Hint,
                Created_at = new DateTimeOffset(created.CreatedAt, TimeSpan.Zero),
                Plain_key = created.PlainKey,
                Scopes = ApiKeyScopes.Split(created.Scopes).ToList()
            });
        });

        admin.MapDelete("/api-keys/{id:guid}", async Task<Results<Ok<StatusResponse>, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>>> (
            Guid id,
            IExecutionContextAccessor ctx,
            [FromServices] IApiCredentialService credentials) =>
        {
            try
            {
                await credentials.RevokeAsync(ctx.TenantId, id);
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

        admin.MapGet("/workspaces/{id:guid}/lhdn-config", async Task<Results<Ok<LhdnTenantConfigDto>, NotFound, UnauthorizedHttpResult>> (
            Guid id,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            if (ctx.TenantId != id && !ctx.IsSystemAdmin) return TypedResults.Unauthorized();

            var result = await mediator.Send(new GetLhdnTenantConfigQuery(id));
            return result != null ? TypedResults.Ok(result) : TypedResults.NotFound();
        });

        admin.MapPut("/workspaces/{id:guid}/lhdn-config", async Task<Results<Ok<StatusResponse>, BadRequest<Microsoft.AspNetCore.Mvc.ProblemDetails>, UnauthorizedHttpResult>> (
            Guid id,
            [FromBody] UpdateLhdnTenantConfigRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            if (ctx.TenantId != id && !ctx.IsSystemAdmin) return TypedResults.Unauthorized();

            try
            {
                await mediator.Send(new UpdateLhdnTenantConfigCommand(id, req));
                return TypedResults.Ok(new StatusResponse { Status = "updated" });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new Microsoft.AspNetCore.Mvc.ProblemDetails { Status = 400, Detail = ex.Message });
            }
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
