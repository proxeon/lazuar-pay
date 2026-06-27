using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Modules.Commerce.Infrastructure;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapCommerceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var adminGroup = endpoints.MapGroup("/admin/commerce").RequireAuthorization("OrgAdmin");
        var publicGroup = endpoints.MapGroup("/public/commerce");

        adminGroup.MapProductEndpoints();
        publicGroup.MapPublicCommerceEndpoints();

        return endpoints;
    }
}
