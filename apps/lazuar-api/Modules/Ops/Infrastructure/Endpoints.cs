// apps/lazuar-api/Modules/Ops/Infrastructure/Endpoints.cs
using System;
using System.Text.Json;
using System.Text.Json.Nodes;
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
using ProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

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

        group.MapPost("/chat/stream", async Task ([FromBody] ChatRequestDto request, ILlmOrchestratorService orchestrator, HttpContext ctx) => 
        {
            ctx.Response.Headers.Append("Content-Type", "text/event-stream");
            ctx.Response.Headers.Append("Cache-Control", "no-cache");
            ctx.Response.Headers.Append("Connection", "keep-alive");

            var stream = orchestrator.ProcessChatStreamAsync(request.Message, ctx.RequestAborted);
            
            await foreach (var chunk in stream)
            {
                var json = JsonSerializer.Serialize(chunk, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
                await ctx.Response.WriteAsync($"data: {json}\n\n");
                await ctx.Response.Body.FlushAsync();
            }

            await ctx.Response.WriteAsync("data: [DONE]\n\n");
            await ctx.Response.Body.FlushAsync();
        });

        group.MapPost("/execute-action", async Task<IResult> (
            [FromBody] ProposedActionDto request, 
            IMemoryCache cache, 
            IToolRegistry toolRegistry, 
            IMediator mediator, 
            IExecutionContextAccessor executionCtx,
            HttpContext httpContext) => 
        {
            if (cache.TryGetValue(request.Idempotency_key, out _))
            {
                return Results.Ok(new StatusResponse { Status = "already_processed" });
            }
            cache.Set(request.Idempotency_key, true, TimeSpan.FromMinutes(5));

            var toolDefinition = toolRegistry.GetToolDefinition(request.Tool_name);
            if (toolDefinition == null || !toolDefinition.IsWriteCommand)
            {
                return Results.BadRequest(new ProblemDetails { Status = 400, Detail = "Invalid or missing write command tool definition." });
            }

            var jsonNode = JsonSerializer.SerializeToNode(request.Command_payload) as JsonObject;
            if (jsonNode == null) return Results.BadRequest(new ProblemDetails { Status = 400, Detail = "Invalid payload." });

            // Ensure the write action executes against the correct Tenant ID (fallback to dev org if header missing)
            var tenantId = executionCtx.TenantId == Guid.Empty ? Guid.Parse("7d97963c-063c-4598-86cc-9ddd9d47d9b1") : executionCtx.TenantId;
            jsonNode["OrganizationId"] = tenantId;

            httpContext.Items["IsAgentAction"] = true;

            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var command = jsonNode.Deserialize(toolDefinition.RequestType, options);
                
                await mediator.Send(command!);

                return Results.Ok(new StatusResponse { Status = "action_executed" });
            }
            catch (Exception ex)
            {
                cache.Remove(request.Idempotency_key);
                // Return exactly the inner exception message to feed it back into the AI
                return Results.BadRequest(new ProblemDetails { Status = 400, Detail = ex.InnerException?.Message ?? ex.Message });
            }
        });

        return endpoints;
    }
}
