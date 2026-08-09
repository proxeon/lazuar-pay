using Microsoft.AspNetCore.Routing;

namespace Modules.Commerce.Infrastructure;

/// <summary>
/// Composer for public commerce routes. Domain maps live in Public*Endpoints files.
/// </summary>
public static class PublicEndpoints
{
    public static RouteGroupBuilder MapPublicCommerceEndpoints(this RouteGroupBuilder group)
    {
        group.MapPublicProductEndpoints();
        group.MapPublicPortalEndpoints();
        group.MapPublicCheckoutEndpoints();
        group.MapPublicCustomCheckoutEndpoints();
        group.MapPublicArrearsEndpoints();

        return group;
    }
}
