using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Modules.Lhdn.Infrastructure;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapLhdnEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapLhdnDocumentEndpoints();
        endpoints.MapLhdnAdminApiKeyEndpoints();
        endpoints.MapLhdnAdminWebhookEndpoints();
        endpoints.MapLhdnTenantConfigEndpoints();

        return endpoints;
    }
}
