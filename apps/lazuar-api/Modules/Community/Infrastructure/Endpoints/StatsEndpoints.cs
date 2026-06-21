// apps/lazuar-api/Modules/Community/Infrastructure/Endpoints/StatsEndpoints.cs
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Modules.Community.Application.Queries;

namespace Modules.Community.Infrastructure.Endpoints;

public static class StatsEndpoints
{
    public static RouteGroupBuilder MapStatsEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/stats", async Task<Ok<CommunitySubscriberStatsDto>> (
            IExecutionContextAccessor ctx,
            ICommunityQueryService queryService) =>
        {
            var stats = await queryService.GetSubscriberStatsAsync(ctx.TenantId);
            return TypedResults.Ok(stats);
        });

        return group;
    }
}
