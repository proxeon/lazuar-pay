using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Modules.Billing.Contracts;
using Modules.Payments.Contracts.Queries;

namespace Modules.Billing.Infrastructure;

public static class AdminCreditsEndpoints
{
    public static RouteGroupBuilder MapAdminCreditsEndpoints(this RouteGroupBuilder admin)
    {
        admin.MapGet("/credits", async Task<Ok<CreditBalanceDto>> (
            IExecutionContextAccessor ctx,
            IBillingQueryService queryService) =>
        {
            var balance = await queryService.GetCreditBalanceWithHistoryAsync(ctx.TenantId);
            return TypedResults.Ok(balance);
        });

        admin.MapGet("/credits/packages", (
            ICreditCostService creditCostService) =>
        {
            var packages = creditCostService.GetPackages()
                .Select(p => new CreditPackageDto { Amount_myr = (double)p.AmountMyr, Credits = p.Credits })
                .ToList();
            return TypedResults.Ok(packages);
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

        return admin;
    }
}
