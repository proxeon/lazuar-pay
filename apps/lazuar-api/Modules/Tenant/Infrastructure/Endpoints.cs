using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;
using Modules.Tenant.Contracts;

namespace Modules.Tenant.Infrastructure;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapTenantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/tenant");

        group.MapGet("/{id:guid}", async (Guid id, ITenantQueryService queryService) =>
        {
            var tenant = await queryService.GetTenantByIdAsync(id);
            return tenant != null ? Results.Ok(tenant) : Results.NotFound();
        });

        return endpoints;
    }
}
