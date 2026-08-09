using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Modules.Ops.Infrastructure;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapOpsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/ops").RequireAuthorization(policy => policy.RequireRole("CLIENT", "ADMIN"));

        group.MapOpsChatEndpoints();
        group.MapOpsChatStreamEndpoints();
        group.MapOpsExecuteActionEndpoints();

        return endpoints;
    }
}
