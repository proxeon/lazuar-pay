// apps/lazuar-api/Modules/One/Infrastructure/Endpoints.cs
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Modules.One.Infrastructure;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapOneEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/one").RequireCors();

        group.MapAuthEndpoints();
        group.MapProfileEndpoints();
        group.MapWorkspaceEndpoints();
        group.MapWebhookEndpoints();
        group.MapStorageEndpoints();
        group.MapApiCredentialEndpoints();
        group.MapIntegrationProvisionEndpoints();

        endpoints.MapPublicWorkspaceBrandingEndpoints();

        return endpoints;
    }
}
