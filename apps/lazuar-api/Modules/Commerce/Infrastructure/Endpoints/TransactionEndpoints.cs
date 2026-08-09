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
using Modules.Commerce.Application.Queries;
using Modules.Commerce.Contracts.Commands;

namespace Modules.Commerce.Infrastructure;

public static class TransactionEndpoints
{
    public static RouteGroupBuilder MapTransactionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/transactions", async Task<Ok<PaginatedResponse<TransactionLogDto>>> (
            [FromQuery] string? search,
            [FromQuery] int page,
            [FromQuery] int limit,
            [FromQuery] string? status,
            [FromQuery] string? payment_method,
            IExecutionContextAccessor ctx,
            ICommerceQueryService queryService) =>
        {
            var p = page < 1 ? 1 : page;
            var l = limit < 1 || limit > 100 ? 50 : limit;
            var response = await queryService.GetTransactionsAsync(ctx.TenantId, p, l, status, payment_method, search);
            return TypedResults.Ok(response);
        });

        // Thin ops endpoint: publishes GatewayRefundRequested for Payments to execute at the gateway.
        group.MapPost("/transactions/{id:guid}/refund", async Task<Results<Ok<StatusResponse>, BadRequest<StatusResponse>>> (
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

                await mediator.Send(new RecordRefundCommand(
                    ctx.TenantId,
                    id,
                    amount,
                    req?.Gateway_name,
                    subscriptionId,
                    taxAmount));

                return TypedResults.Ok(new StatusResponse { Status = "refund_requested" });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(new StatusResponse { Status = ex.Message });
            }
        });

        return group;
    }
}
