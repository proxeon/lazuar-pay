using System.Text.Json;
using System.Threading.Tasks;
using Lazuar.ApiTypes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modules.Ops.Application.Services;

namespace Modules.Ops.Infrastructure;

public static class ChatStreamEndpoints
{
    public static RouteGroupBuilder MapOpsChatStreamEndpoints(this RouteGroupBuilder group)
    {
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

        return group;
    }
}
