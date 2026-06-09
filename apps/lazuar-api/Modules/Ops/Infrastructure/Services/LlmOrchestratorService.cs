// apps/lazuar-api/Modules/Ops/Infrastructure/Services/LlmOrchestratorService.cs

// ==============================================================================================
// DONT DELETE COMMENT HERE. IF you need to modify, just modify specific code and dont delete the comment.
// 
// HISTORICAL BUG CONTEXT:
// We previously experienced hard 500 Internal Server Errors because of how the new OpenAI SDK (v2+) 
// streams tool arguments as `BinaryData`. Implicitly appending `BinaryData` to a `string` 
// (e.g., `acc.Arguments += update`) calls `.ToString()` on partial UTF-8 byte chunks. 
// If a multi-byte character or JSON structural boundary gets split across two network chunks, 
// it results in a corrupted string. When passed to JsonSerializer, it threw an unhandled 
// JsonException BEFORE the response headers could be flushed, crashing the Minimal API.
// 
// FIX IMPLEMENTED & REQUIRED TO MAINTAIN:
// 1. Accumulate raw bytes using `MemoryStream` in `ToolCallAccumulator` rather than `string`.
// 2. Decode to a UTF-8 string ONLY when the stream completes.
// 3. Wrap `JsonSerializer.Deserialize` in a try/catch to catch LLM JSON hallucinations gracefully.
// ==============================================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Application.Llm;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.Extensions.Logging;
using Modules.Ops.Application.Services;
using OpenAI.Chat;

namespace Modules.Ops.Infrastructure.Services;

public class LlmOrchestratorService : ILlmOrchestratorService
{
    private readonly IChatClientFactory _clientFactory;
    private readonly IToolRegistry _toolRegistry;
    private readonly IMediator _mediator;
    private readonly IExecutionContextAccessor _executionContext;
    private readonly ILogger<LlmOrchestratorService> _logger;

    public LlmOrchestratorService(
        IChatClientFactory clientFactory,
        IToolRegistry toolRegistry,
        IMediator mediator,
        IExecutionContextAccessor executionContext,
        ILogger<LlmOrchestratorService> logger)
    {
        _clientFactory = clientFactory;
        _toolRegistry = toolRegistry;
        _mediator = mediator;
        _executionContext = executionContext;
        _logger = logger;
    }

    public async Task<ChatResponseDto> ProcessChatAsync(string userMessage, CancellationToken ct = default)
    {
        var messages = BuildInitialMessages(userMessage);
        var chatClient = _clientFactory.CreateClient();
        var completion = await chatClient.CompleteChatAsync(messages, BuildChatOptions(), ct);
        return new ChatResponseDto { Message = completion.Value.Content[0].Text };
    }

    public async IAsyncEnumerable<ChatStreamChunkDto> ProcessChatStreamAsync(string userMessage, [EnumeratorCancellation] CancellationToken ct = default)
    {
        List<ChatMessage>? messages = null;
        ChatCompletionOptions? options = null;
        ChatClient? chatClient = null;
        Exception? initError = null;

        try
        {
            messages = BuildInitialMessages(userMessage);
            options = BuildChatOptions();
            chatClient = _clientFactory.CreateClient();
        }
        catch (Exception ex)
        {
            initError = ex;
        }

        if (initError != null)
        {
            yield return new ChatStreamChunkDto { Type = "text", Content = $"⚠️ **System Configuration Error:**\n\n`{initError.Message}`\n\nEnsure your OpenRouter API Key is set correctly." };
            yield break;
        }

        int maxIterations = 3;
        int iterations = 0;

        while (iterations < maxIterations)
        {
            var toolCallAccumulators = new Dictionary<int, ToolCallAccumulator>();
            var hasToolCalls = false;
            var textAppended = false;
            ChatTokenUsage? finalUsage = null;
            Exception? streamError = null;
            Exception? requestError = null;

            IAsyncEnumerator<StreamingChatCompletionUpdate>? enumerator = null;

            try
            {
                var streamingResult = chatClient!.CompleteChatStreamingAsync(messages!, options!, ct);
                enumerator = streamingResult.GetAsyncEnumerator(ct);
            }
            catch (Exception ex)
            {
                requestError = ex;
            }

            if (requestError != null)
            {
                yield return new ChatStreamChunkDto { Type = "text", Content = $"\n\n⚠️ **LLM Request Error:**\n\n`{requestError.Message}`" };
                yield break;
            }

            try 
            {
                while (true)
                {
                    bool hasNext;
                    try
                    {
                        hasNext = await enumerator!.MoveNextAsync();
                    }
                    catch (Exception ex)
                    {
                        streamError = ex;
                        break;
                    }

                    if (!hasNext) break;

                    var update = enumerator.Current;
                    if (update.Usage != null) finalUsage = update.Usage;

                    if (update.ContentUpdate.Count > 0 && !string.IsNullOrEmpty(update.ContentUpdate[0].Text))
                    {
                        textAppended = true;
                        yield return new ChatStreamChunkDto { Type = "text", Content = update.ContentUpdate[0].Text };
                    }

                    foreach (var toolUpdate in update.ToolCallUpdates)
                    {
                        hasToolCalls = true;
                        if (!toolCallAccumulators.TryGetValue(toolUpdate.Index, out var acc))
                        {
                            acc = new ToolCallAccumulator { Id = toolUpdate.ToolCallId ?? Guid.NewGuid().ToString() };
                            toolCallAccumulators[toolUpdate.Index] = acc;
                        }
                        if (toolUpdate.FunctionName != null) acc.Name += toolUpdate.FunctionName;
                        
                        if (toolUpdate.FunctionArgumentsUpdate != null)
                        {
                            var bytes = toolUpdate.FunctionArgumentsUpdate.ToArray();
                            acc.ArgumentsStream.Write(bytes, 0, bytes.Length);
                        }
                    }
                }
            }
            finally 
            {
                if (enumerator != null) await enumerator.DisposeAsync();
            }

            if (streamError != null)
            {
                yield return new ChatStreamChunkDto { Type = "text", Content = $"\n\n⚠️ **LLM Streaming Error:**\n\n`{streamError.Message}`" };
                yield break;
            }

            TrackAndLogCost(finalUsage);

            if (hasToolCalls)
            {
                var toolCalls = toolCallAccumulators.Values
                    .Select(a => 
                    {
                        var jsonArgs = Encoding.UTF8.GetString(a.ArgumentsStream.ToArray());
                        return ChatToolCall.CreateFunctionToolCall(a.Id, a.Name, BinaryData.FromString(jsonArgs));
                    })
                    .ToList();

                foreach (var acc in toolCallAccumulators.Values)
                {
                    acc.Dispose();
                }

                messages!.Add(new AssistantChatMessage(toolCalls));

                foreach (var toolCall in toolCalls)
                {
                    var definition = _toolRegistry.GetToolDefinition(toolCall.FunctionName);
                    if (definition == null)
                    {
                        messages.Add(new ToolChatMessage(toolCall.Id, "Error: Tool not found."));
                        continue;
                    }

                    if (definition.IsWriteCommand)
                    {
                        var proposedAction = BuildProposedAction(definition, toolCall.FunctionArguments.ToString());
                        yield return new ChatStreamChunkDto { Type = "proposed_action", Proposed_action = proposedAction };
                        yield break; 
                    }

                    yield return new ChatStreamChunkDto { Type = "tool_status", Tool_name = definition.Name, Content = "Fetching data..." };

                    var resultJson = await ExecuteReadToolAsync(definition, toolCall.FunctionArguments.ToString(), ct);
                    messages.Add(new ToolChatMessage(toolCall.Id, resultJson));
                }

                iterations++;
            }
            else
            {
                if (!textAppended) yield return new ChatStreamChunkDto { Type = "text", Content = "An unknown error occurred." };
                yield break;
            }
        }

        yield return new ChatStreamChunkDto { Type = "text", Content = "\n\nExecution limit reached. Please refine your request." };
    }

    private void TrackAndLogCost(ChatTokenUsage? usage)
    {
        if (usage == null) return;
        _logger.LogInformation("FinOps [Tenant: {TenantId}] - Input: {Input}, Output: {Output}", 
            _executionContext.TenantId, usage.InputTokenCount, usage.OutputTokenCount);
    }

    private List<ChatMessage> BuildInitialMessages(string userMessage)
    {
        var tenantId = _executionContext.TenantId == Guid.Empty ? Guid.Parse("7d97963c-063c-4598-86cc-9ddd9d47d9b1") : _executionContext.TenantId;

        return new List<ChatMessage>
        {
            new SystemChatMessage(
                $"You are Lazuar Ops, a highly capable internal operations agent. " +
                $"The current OrganizationId is {tenantId}. " +
                $"**CRITICAL RULE 1**: You must ALWAYS use search tools (like SearchSubscribersAgentQuery or ListActivePlansAgentQuery) to find exact GUID identifiers before executing any write commands. Never guess or hallucinate a Guid. " +
                $"**CRITICAL RULE 2**: If you need to generate a URL or need to know the 'tenant slug', run the GetWorkspaceDetailsAgentQuery tool first to retrieve it. " +
                $"If the user asks you to perform a write action, strictly call the relevant tool. " +
                $"Do not chain multiple write tools in a single turn. " +
                $"For read operations, you may call multiple tools to gather the necessary context."
            ),
            new UserChatMessage(userMessage)
        };
    }

    private ChatCompletionOptions BuildChatOptions()
    {
        var options = new ChatCompletionOptions();
        var tools = _toolRegistry.GetAvailableTools(_executionContext.UserRole).ToList();
        
        if (tools.Any())
        {
            foreach (var tool in tools) options.Tools.Add(tool.ChatTool);
        }
        
        return options;
    }

    private ProposedActionDto BuildProposedAction(AgentToolDefinition definition, string arguments)
    {
        object payload;
        try 
        {
            payload = JsonSerializer.Deserialize<object>(arguments) ?? new object();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "LLM generated malformed JSON for tool {ToolName}. Raw arguments: {Arguments}", definition.Name, arguments);
            
            payload = new { 
                _error = "The AI generated invalid parameters.", 
                _raw_output = arguments 
            };
        }

        return new ProposedActionDto
        {
            Idempotency_key = Guid.CreateVersion7().ToString(),
            Tool_name = definition.Name,
            Intent_title = FormatIntentTitle(definition.Name),
            Severity = definition.Severity,
            Human_readable_summary = $"Proposing to execute {FormatIntentTitle(definition.Name)}.",
            Command_payload = payload
        };
    }

    private async Task<string> ExecuteReadToolAsync(AgentToolDefinition definition, string arguments, CancellationToken ct)
    {
        try
        {
            var jsonNode = JsonSerializer.SerializeToNode(JsonSerializer.Deserialize<object>(arguments)) as JsonObject ?? new JsonObject();
            
            var tenantId = _executionContext.TenantId == Guid.Empty ? Guid.Parse("7d97963c-063c-4598-86cc-9ddd9d47d9b1") : _executionContext.TenantId;
            jsonNode["OrganizationId"] = tenantId;

            var deserializeOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var args = jsonNode.Deserialize(definition.RequestType, deserializeOptions);

            var result = await _mediator.Send(args!, ct);
            return JsonSerializer.Serialize(result);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private static string FormatIntentTitle(string commandName)
    {
        var name = commandName.Replace("Command", "");
        return string.Concat(name.Select(x => char.IsUpper(x) ? " " + x : x.ToString())).TrimStart();
    }

    private class ToolCallAccumulator : IDisposable
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public MemoryStream ArgumentsStream { get; } = new MemoryStream();

        public void Dispose() => ArgumentsStream.Dispose();
    }
}
