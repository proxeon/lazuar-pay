using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modules.Commerce.Application.Queries;

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

        return group;
    }
}
