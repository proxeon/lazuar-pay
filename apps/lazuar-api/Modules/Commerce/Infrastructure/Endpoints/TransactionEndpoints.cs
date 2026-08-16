using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modules.Commerce.Application;
using Modules.Commerce.Application.Queries;
using Modules.Commerce.Contracts.Commands;
using ProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

namespace Modules.Commerce.Infrastructure;

public static class TransactionEndpoints
{
    public static RouteGroupBuilder MapTransactionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/transactions/export", async (
            [FromQuery] DateTimeOffset? from,
            [FromQuery] DateTimeOffset? to,
            [FromQuery] string? status,
            IExecutionContextAccessor ctx,
            ICommerceQueryService queryService,
            HttpResponse response) =>
        {
            var toUtc = (to ?? DateTimeOffset.UtcNow).UtcDateTime;
            var fromUtc = (from ?? new DateTimeOffset(toUtc.AddDays(-31), TimeSpan.Zero)).UtcDateTime;
            if (fromUtc > toUtc)
            {
                (fromUtc, toUtc) = (toUtc, fromUtc);
            }

            var (rows, truncated) = await queryService.ExportTransactionsAsync(
                ctx.TenantId, fromUtc, toUtc, status);
            if (truncated)
            {
                response.Headers["X-Export-Truncated"] = "true";
            }

            var csv = TransactionExportCsv.Build(rows);
            var bytes = TransactionExportCsv.ToUtf8Bom(csv);
            var name = $"transactions_{fromUtc:yyyyMMdd}_{toUtc:yyyyMMdd}.csv";
            return Results.File(bytes, "text/csv", name);
        });

        group.MapGet("/transactions", async Task<Ok<PaginatedResponse<TransactionLogDto>>> (
            [FromQuery] string? search,
            [FromQuery] int page,
            [FromQuery] int limit,
            [FromQuery] string? status,
            [FromQuery] string? payment_method,
            [FromQuery] string? gateway_name,
            [FromQuery] string? subscription_id,
            IExecutionContextAccessor ctx,
            ICommerceQueryService queryService) =>
        {
            var p = page < 1 ? 1 : page;
            var l = limit < 1 || limit > 100 ? 50 : limit;
            _ = payment_method;
            Guid? subscriptionId = Guid.TryParse(subscription_id, out var sid) ? sid : null;
            var response = await queryService.GetTransactionsAsync(ctx.TenantId, p, l, status, gateway_name, search, subscriptionId);
            return TypedResults.Ok(response);
        });

        group.MapPost("/transactions/{id:guid}/refund", async Task<Results<Ok<StatusResponse>, BadRequest<ProblemDetails>>> (
            Guid id,
            RecordRefundRequestDto? req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            try
            {
                Guid? subscriptionId = null;
                if (!string.IsNullOrWhiteSpace(req?.Subscription_id) && Guid.TryParse(req.Subscription_id, out var sid))
                {
                    subscriptionId = sid;
                }

                decimal? amount = req?.Amount is double a ? (decimal)a : null;
                decimal taxAmount = req?.Tax_amount is double t ? (decimal)t : 0m;

                var status = await mediator.Send(new RecordRefundCommand(
                    ctx.TenantId,
                    id,
                    amount,
                    req?.Gateway_name,
                    subscriptionId,
                    taxAmount,
                    req?.Mark_refunded == true,
                    req?.Reason));

                return TypedResults.Ok(new StatusResponse { Status = status });
            }
            catch (RefundRejectedException ex)
            {
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Status = 400,
                    Title = ex.Code,
                    Detail = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Status = 400,
                    Title = "REFUND_REJECTED",
                    Detail = ex.Message
                });
            }
        });

        return group;
    }
}
