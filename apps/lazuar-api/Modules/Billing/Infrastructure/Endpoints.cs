using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using BuildingBlocks.Application;
using Modules.Billing.Contracts;
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

        return endpoints;
    }
}
