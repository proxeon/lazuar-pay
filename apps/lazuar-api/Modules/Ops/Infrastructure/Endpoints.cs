using System;
using System.Linq;
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
using Modules.Ops.Application;
using Modules.Ops.Application.Services;
using Modules.Ops.Domain;
using ProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

namespace Modules.Ops.Infrastructure;

public static class Endpoints
{
    public static IEndpointRouteBuilder MapOpsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/ops").RequireAuthorization(policy => policy.RequireRole("CLIENT", "ADMIN"));

        group.MapGet("/chat/conversations", async Task<IResult> ([FromQuery] int limit, [FromQuery] int offset, IOpsRepository repo, IExecutionContextAccessor ctx) => 
        {
            var tenantId = ctx.TenantId;
            if (tenantId == Guid.Empty) return Results.BadRequest(new ProblemDetails { Status = 400, Detail = "Active workspace context required." });
            
            int safeLimit = limit > 0 ? limit : 20;
            int safeOffset = offset >= 0 ? offset : 0;
            
            var conversations = await repo.GetConversationsAsync(tenantId, safeLimit, safeOffset);
            
            var dtos = conversations.Select(c => new OpsConversationDto {
                Id = c.Id.ToString(),
                Title = c.Title,
                Updated_at = new DateTimeOffset(c.UpdatedAt)
            }).ToList();
            
            int currentPage = (safeOffset / safeLimit) + 1;
            return Results.Ok(new PaginatedResponse<OpsConversationDto>(dtos, 0, currentPage, safeLimit));
        });

        group.MapGet("/chat/conversations/{id:guid}/messages", async Task<IResult> (Guid id, IOpsRepository repo, IExecutionContextAccessor ctx) => 
        {
            var tenantId = ctx.TenantId;
            if (tenantId == Guid.Empty) return Results.BadRequest(new ProblemDetails { Status = 400, Detail = "Active workspace context required." });
            
            var messages = await repo.GetMessagesAsync(tenantId, id);
            
            var dtos = messages.Select(m => new OpsMessageDto {
                Id = m.Id.ToString(),
                Conversation_id = m.ConversationId.ToString(),
                Role = m.Role,
                Content = m.Content,
                Tool_status = m.ToolStatus,
                Proposed_action = m.ProposedActionJson != null 
                    ? JsonSerializer.Deserialize<ProposedActionDto>(m.ProposedActionJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) 
                    : null,
                Created_at = new DateTimeOffset(m.CreatedAt)
            }).ToList();
            
            return Results.Ok(dtos);
        });

        group.MapPost("/chat", async Task<IResult> ([FromBody] ChatRequestDto request, ILlmOrchestratorService orchestrator) => 
        {
            var response = await orchestrator.ProcessChatAsync(request.Message, request.Conversation_id);
            return Results.Ok(response);
        });

        group.MapPost("/chat/stream", async Task ([FromBody] ChatRequestDto request, ILlmOrchestratorService orchestrator, HttpContext ctx) => 
        {
            ctx.Response.Headers.Append("Content-Type", "text/event-stream");
            ctx.Response.Headers.Append("Cache-Control", "no-cache");
            ctx.Response.Headers.Append("Connection", "keep-alive");

            var stream = orchestrator.ProcessChatStreamAsync(request.Message, request.Conversation_id, ctx.RequestAborted);
            
            await foreach (var chunk in stream)
            {
                var json = JsonSerializer.Serialize(chunk, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
                await ctx.Response.WriteAsync($"data: {json}\n\n");
                await ctx.Response.Body.FlushAsync();
            }

            await ctx.Response.WriteAsync("data: [DONE]\n\n");
            await ctx.Response.Body.FlushAsync();
        });

        group.MapPost("/chat/conversations/{id:guid}/system-message", async Task<IResult> (Guid id, [FromBody] ChatRequestDto request, IOpsRepository repo, IExecutionContextAccessor ctx) => 
        {
            var tenantId = ctx.TenantId;
            if (tenantId == Guid.Empty) return Results.BadRequest(new ProblemDetails { Status = 400, Detail = "Active workspace context required." });
            
            var conv = await repo.GetConversationByIdAsync(tenantId, id);
            if (conv == null) return Results.NotFound();

            repo.AddMessage(new OpsMessage(Guid.CreateVersion7(), tenantId, id, "system", request.Message));
            await repo.SaveChangesAsync();
            
            return Results.Ok(new StatusResponse { Status = "saved" });
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

            var tenantId = executionCtx.TenantId;
            if (tenantId == Guid.Empty) return Results.BadRequest(new ProblemDetails { Status = 400, Detail = "Active workspace context required." });
            
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
                return Results.BadRequest(new ProblemDetails { Status = 400, Detail = ex.InnerException?.Message ?? ex.Message });
            }
        });

        return endpoints;
    }
}
