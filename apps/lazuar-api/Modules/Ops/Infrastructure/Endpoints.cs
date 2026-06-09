using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using MediatR;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using Modules.Ops.Application.Services;

namespace Modules.Ops.Infrastructure;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapOpsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/ops").RequireAuthorization();

        group.MapPost("/chat", async Task<IResult> ([FromBody] ChatRequestDto request, ILlmOrchestratorService orchestrator) => 
        {
            var response = await orchestrator.ProcessChatAsync(request.Message);
            return Results.Ok(response);
        });

        group.MapPost("/execute-action", async Task<IResult> (
            [FromBody] ProposedActionDto request, 
            IMemoryCache cache, 
            IToolRegistry toolRegistry, 
            IMediator mediator, 
            IExecutionContextAccessor ctx,
            HttpContext httpContext) => 
        {
            // 1. Idempotency Check (5 Minute TTL to prevent double-clicks)
            if (cache.TryGetValue(request.Idempotency_key, out _))
            {
                return Results.Ok(new StatusResponse { Status = "already_processed" });
            }
            cache.Set(request.Idempotency_key, true, TimeSpan.FromMinutes(5));

            // 2. Resolve Command Type
            var toolDefinition = toolRegistry.GetToolDefinition(request.Tool_name);
            if (toolDefinition == null || !toolDefinition.IsWriteCommand)
            {
                return Results.BadRequest(new ProblemDetails { Status = 400, Detail = "Invalid or missing write command tool definition." });
            }

            // 3. Inject Context & Audit Boundaries Programmatically
            var jsonNode = JsonSerializer.SerializeToNode(request.Command_payload) as System.Text.Json.Nodes.JsonObject;
            if (jsonNode == null) return Results.BadRequest();

            jsonNode["OrganizationId"] = ctx.TenantId;
            jsonNode["RecordedBy"] = ctx.AuditSignature;

            // Mark Request Context as Agent Action for deeper audit interceptors downstream
            httpContext.Items["IsAgentAction"] = true;

            try
            {
                // 4. Deserialize into strongly-typed MediatR record & Dispatch
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var command = jsonNode.Deserialize(toolDefinition.RequestType, options);
                
                await mediator.Send(command!);

                return Results.Ok(new StatusResponse { Status = "action_executed" });
            }
            catch (Exception ex)
            {
                // Un-set idempotency key if transaction failed so human can try modifying and submitting again
                cache.Remove(request.Idempotency_key);
                return Results.BadRequest(new ProblemDetails { Status = 400, Detail = ex.Message });
            }
        });

        return endpoints;
    }
}
