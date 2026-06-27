using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modules.Community.Application.Queries;
using Modules.One.Contracts;

namespace Modules.Community.Infrastructure;

public static class PublicEndpoints
{
    public static RouteGroupBuilder MapPublicCommunityEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/{tenantSlug}/portal/spaces", async Task<Results<Ok<ICollection<PortalCommunitySpaceDto>>, NotFound>> (
            string tenantSlug,
            [FromQuery] string[] product_ids,
            IOneQueryService oneQueryService,
            ICommunityQueryService queryService) =>
        {
            var tenantId = await oneQueryService.GetTenantIdBySlugAsync(tenantSlug);
            if (!tenantId.HasValue) return TypedResults.NotFound();

            var parsedIds = product_ids.Select(id => Guid.TryParse(id, out var parsed) ? parsed : Guid.Empty).Where(id => id != Guid.Empty).ToList();
            
            var spaces = await queryService.GetPortalSpacesAsync(tenantId.Value, parsedIds);
            
            return TypedResults.Ok((ICollection<PortalCommunitySpaceDto>)spaces.ToList());
        });

        return group;
    }
}
