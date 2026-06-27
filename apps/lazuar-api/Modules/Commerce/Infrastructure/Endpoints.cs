using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Modules.Commerce.Infrastructure;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapCommerceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Endpoints will be orchestrated by BFF endpoints in future phases
        return endpoints;
    }
}
