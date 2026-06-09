using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Lazuar.ApiTypes;

namespace Modules.Ops.Infrastructure;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapOpsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/ops").RequireAuthorization();

        group.MapPost("/chat", (HttpContext context) => 
        {
            return Results.Ok(new StatusResponse { Status = "stream_initialized" });
        });

        group.MapPost("/execute-action", (HttpContext context) => 
        {
            return Results.Ok(new StatusResponse { Status = "action_executed" });
        });

        return endpoints;
    }
}
