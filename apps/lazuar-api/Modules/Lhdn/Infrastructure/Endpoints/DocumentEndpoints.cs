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

public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapLhdnDocumentEndpoints(this IEndpointRouteBuilder endpoints)
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

        return endpoints;
    }
}
