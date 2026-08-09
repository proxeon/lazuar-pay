using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modules.Ops.Application;
using Modules.Ops.Application.Commands;
using Modules.Ops.Application.Services;
using Modules.Ops.Domain;

namespace Modules.Ops.Infrastructure;

public static class ChatEndpoints
{
    public static RouteGroupBuilder MapOpsChatEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/chat/conversations", async Task<IResult> ([FromQuery] int limit, [FromQuery] int offset, IOpsRepository repo, IExecutionContextAccessor ctx) =>
        {
            var tenantId = ctx.TenantId;
            if (tenantId == Guid.Empty) throw new InvalidOperationException("Active workspace context required.");

            // Legacy limit/offset query shape (not page/limit); still returns truthful TotalCount for UI paging.
            var (safeLimit, safeOffset, currentPage) = Paging.NormalizeOffset(limit, offset, defaultLimit: 20);

            var conversations = await repo.GetConversationsAsync(tenantId, safeLimit, safeOffset);
            var totalCount = await repo.CountConversationsAsync(tenantId);

            var dtos = conversations.Select(c => new OpsConversationDto
            {
                Id = c.Id.ToString(),
                Title = c.Title,
                Updated_at = new DateTimeOffset(c.UpdatedAt)
            }).ToList();

            return Results.Ok(new PaginatedResponse<OpsConversationDto>(dtos, totalCount, currentPage, safeLimit));
        });

        group.MapGet("/chat/conversations/{id:guid}/messages", async Task<IResult> (Guid id, IOpsRepository repo, IExecutionContextAccessor ctx) =>
        {
            var tenantId = ctx.TenantId;
            if (tenantId == Guid.Empty) throw new InvalidOperationException("Active workspace context required.");

            var messages = await repo.GetMessagesAsync(tenantId, id);

            var dtos = messages.Select(m => new OpsMessageDto
            {
                Id = m.Id.ToString(),
                Conversation_id = m.ConversationId.ToString(),
                Role = m.Role,
                Content = m.Content,
                Executed_tools = ParseExecutedToolsSafe(m.ExecutedToolsJson),
                Proposed_action = m.ProposedActionJson != null
                    ? JsonSerializer.Deserialize<ProposedActionDto>(m.ProposedActionJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    : null,
                Ui_request = m.UiRequestJson != null
                    ? JsonSerializer.Deserialize<UiRequestDto>(m.UiRequestJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    : null,
                Is_resolved = m.IsResolved,
                Created_at = new DateTimeOffset(m.CreatedAt)
            }).ToList();

            return Results.Ok(dtos);
        });

        group.MapPost("/chat", async Task<IResult> ([FromBody] ChatRequestDto request, ILlmOrchestratorService orchestrator) =>
        {
            var response = await orchestrator.ProcessChatAsync(request.Message, request.Conversation_id);
            return Results.Ok(response);
        });

        group.MapPost("/chat/conversations/{id:guid}/system-message", async Task<IResult> (Guid id, [FromBody] ChatRequestDto request, IOpsRepository repo, IExecutionContextAccessor ctx) =>
        {
            var tenantId = ctx.TenantId;
            if (tenantId == Guid.Empty) throw new InvalidOperationException("Active workspace context required.");

            var conv = await repo.GetConversationByIdAsync(tenantId, id);
            if (conv == null) return Results.NotFound();

            repo.AddMessage(new OpsMessage(Guid.CreateVersion7(), tenantId, id, "system", request.Message));
            await repo.SaveChangesAsync();

            return Results.Ok(new StatusResponse { Status = "saved" });
        });

        group.MapPut("/chat/conversations/{id:guid}/title", async Task<IResult> (Guid id, [FromBody] RenameConversationRequestDto request, IMediator mediator, IExecutionContextAccessor ctx) =>
        {
            var tenantId = ctx.TenantId;
            if (tenantId == Guid.Empty) throw new InvalidOperationException("Active workspace context required.");

            await mediator.Send(new RenameConversationCommand(tenantId, id, request.Title));
            return Results.Ok(new StatusResponse { Status = "updated" });
        });

        group.MapDelete("/chat/conversations/{id:guid}", async Task<IResult> (Guid id, IMediator mediator, IExecutionContextAccessor ctx) =>
        {
            var tenantId = ctx.TenantId;
            if (tenantId == Guid.Empty) throw new InvalidOperationException("Active workspace context required.");

            await mediator.Send(new DeleteConversationCommand(tenantId, id));
            return Results.Ok(new StatusResponse { Status = "deleted" });
        });

        group.MapPut("/chat/messages/{id:guid}/resolve", async Task<IResult> (Guid id, IOpsRepository repo, IExecutionContextAccessor ctx) =>
        {
            var tenantId = ctx.TenantId;
            if (tenantId == Guid.Empty) throw new InvalidOperationException("Active workspace context required.");

            var message = await repo.GetMessageByIdAsync(tenantId, id);
            if (message == null) return Results.NotFound();

            message.ResolveUiRequest();
            await repo.SaveChangesAsync();

            return Results.Ok(new StatusResponse { Status = "resolved" });
        });

        return group;
    }

    internal static List<string>? ParseExecutedToolsSafe(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }
}
