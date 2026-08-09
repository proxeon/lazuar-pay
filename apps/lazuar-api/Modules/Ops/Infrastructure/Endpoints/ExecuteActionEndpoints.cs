using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Modules.Ops.Application.Services;
using ProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

namespace Modules.Ops.Infrastructure;

public static class ExecuteActionEndpoints
{
    public static RouteGroupBuilder MapOpsExecuteActionEndpoints(this RouteGroupBuilder group)
    {
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

            jsonNode["OrganizationId"] = tenantId.ToString();
            jsonNode["RecordedBy"] = executionCtx.AuditSignature;

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

        return group;
    }
}
