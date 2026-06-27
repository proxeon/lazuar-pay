using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Modules.Vault.Infrastructure;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapVaultEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var adminGroup = endpoints.MapGroup("/admin/vault").RequireAuthorization("OrgAdmin");
        var publicGroup = endpoints.MapGroup("/public/vault");
        
        adminGroup.MapAssetEndpoints();
        publicGroup.MapPublicVaultEndpoints();

        return endpoints;
    }
}
