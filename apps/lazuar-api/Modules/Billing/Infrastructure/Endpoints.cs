// apps/lazuar-api/Modules/Billing/Infrastructure/Endpoints.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using BuildingBlocks.Application;
using MediatR;
using Modules.Billing.Contracts;
using Modules.Payments.Contracts.Queries;
using Lazuar.ApiTypes;

namespace Modules.Billing.Infrastructure;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/admin/billing").RequireAuthorization("OrgAdmin");
        
        admin.MapGet("/summary", async Task<Ok<FinancialSummaryDto>> (
            IExecutionContextAccessor ctx,
            IBillingQueryService queryService) =>
        {
            var summary = await queryService.GetFinancialSummaryAsync(ctx.TenantId);
            return TypedResults.Ok(summary);
        });

        admin.MapGet("/credits", async Task<Ok<CreditBalanceDto>> (
            IExecutionContextAccessor ctx,
            IBillingQueryService queryService) =>
        {
            var balance = await queryService.GetCreditBalanceWithHistoryAsync(ctx.TenantId);
            return TypedResults.Ok(balance);
        });

        admin.MapPost("/credits/top-up", async Task<Results<Ok<TopUpResponseDto>, BadRequest<string>>> (
            CreateTopUpRequestDto req,
            IExecutionContextAccessor ctx,
            IMediator mediator) =>
        {
            if (req.Amount_myr < 50) return TypedResults.BadRequest("Minimum top-up amount is RM 50.");

            var metadata = new Dictionary<string, string>
            {
                { "type", "utility_credit_topup" },
                { "tenant_id", ctx.TenantId.ToString() }
            };

            var query = new GenerateSystemCheckoutSessionQuery(
                ctx.TenantId,
                (decimal)req.Amount_myr,
                "MYR",
                "Lazuar Utility Credits",
                "",
                req.Return_url,
                req.Return_url,
                metadata
            );

            try
            {
                var checkoutUrl = await mediator.Send(query);
                return TypedResults.Ok(new TopUpResponseDto { Checkout_url = checkoutUrl });
            }
            catch (Exception ex)
            {
                return TypedResults.BadRequest(ex.Message);
            }
        });

        return endpoints;
    }
}
