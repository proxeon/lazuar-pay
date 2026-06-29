using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Modules.Commerce.Application.Queries;

namespace Modules.Commerce.Infrastructure;

public static class StatsEndpoints
{
    public static RouteGroupBuilder MapStatsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/stats", async Task<Ok<CommerceStatsDto>> (
            IExecutionContextAccessor ctx,
            ICommerceQueryService queryService) =>
        {
            var stats = await queryService.GetStatsAsync(ctx.TenantId);
            return TypedResults.Ok(stats);
        });

        return group;
    }
}
