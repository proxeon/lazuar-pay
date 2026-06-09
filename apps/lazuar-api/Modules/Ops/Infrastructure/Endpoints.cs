using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Lazuar.ApiTypes;
using Modules.Ops.Application.Services;
using System.Threading.Tasks;

namespace Modules.Ops.Infrastructure;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapOpsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/ops").RequireAuthorization();

        group.MapPost("/chat", async Task<IResult> ([FromBody] ChatRequestDto request, ILlmOrchestratorService orchestrator) => 
        {
            var responseText = await orchestrator.ProcessChatAsync(request.Message);
            
            // Phase 2 implementation returns status wrap for now; Phase 4 will transition this to SSE streaming
            return Results.Ok(new StatusResponse { Status = responseText });
        });

        group.MapPost("/execute-action", (HttpContext context) => 
        {
            return Results.Ok(new StatusResponse { Status = "action_executed" });
        });

        return endpoints;
    }
}
