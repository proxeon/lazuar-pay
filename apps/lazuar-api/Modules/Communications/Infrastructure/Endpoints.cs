using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Modules.Communications.Infrastructure;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapCommunicationsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var adminGroup = endpoints.MapGroup("/admin/communications").RequireAuthorization("OrgAdmin");

        adminGroup.MapTemplateEndpoints();
        adminGroup.MapBroadcastEndpoints();

        endpoints.MapPublicComplianceEndpoints();

        return endpoints;
    }
}
