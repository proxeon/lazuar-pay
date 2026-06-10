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
using Microsoft.Extensions.DependencyInjection;
using Modules.Ops.Application;
using Modules.Ops.Application.Services;
using Modules.Ops.Domain;
using OpenAI.Chat;

namespace Modules.Ops.Infrastructure.Services;

public class LlmOrchestratorService : ILlmOrchestratorService
{
    private readonly IChatClientFactory _clientFactory;
    private readonly IToolRegistry _toolRegistry;
    private readonly IMediator _mediator;
    private readonly IExecutionContextAccessor _executionContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LlmOrchestratorService> _logger;

    public LlmOrchestratorService(
        IChatClientFactory clientFactory,
        IToolRegistry toolRegistry,
        IMediator mediator,
        IExecutionContextAccessor executionContext,
        IServiceScopeFactory scopeFactory,
        ILogger<LlmOrchestratorService> logger)
    {
        _clientFactory = clientFactory;
        _toolRegistry = toolRegistry;
        _mediator = mediator;
        _executionContext = executionContext;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<ChatResponseDto> ProcessChatAsync(string userMessage, string? conversationId = null, CancellationToken ct = default)
    {
        var tenantId = GetValidatedTenantId();
        List<OpsMessage> history = new();

        using (var setupScope = _scopeFactory.CreateScope())
        {
            var repo = setupScope.ServiceProvider.GetRequiredService<IOpsRepository>();
            if (!string.IsNullOrEmpty(conversationId) && Guid.TryParse(conversationId, out var convId))
            {
                history = (await repo.GetMessagesAsync(tenantId, convId, ct)).ToList();
            }
        }

        var messages = BuildInitialMessages(tenantId, history, userMessage);
        var chatClient = _clientFactory.CreateClient(thinkingEnabled: true, reasoningEffort: "xhigh");
        var completion = await chatClient.CompleteChatAsync(messages, BuildChatOptions(), ct);

        return new ChatResponseDto { Message = completion.Value.Content[0].Text };
    }

    public async IAsyncEnumerable<ChatStreamChunkDto> ProcessChatStreamAsync(string userMessage, string? conversationId = null, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var tenantId = GetValidatedTenantId();
        List<OpsMessage> history = new();
        Guid convId;
        bool isNew = false;

        using (var setupScope = _scopeFactory.CreateScope())
        {
            var repo = setupScope.ServiceProvider.GetRequiredService<IOpsRepository>();
            var titleGen = setupScope.ServiceProvider.GetRequiredService<ILlmTitleGenerator>();

            if (string.IsNullOrEmpty(conversationId) || !Guid.TryParse(conversationId, out convId))
            {
                isNew = true;
                convId = Guid.CreateVersion7();
                var fallbackTitle = titleGen.GenerateFallback(userMessage);
                repo.AddConversation(new OpsConversation(convId, tenantId, fallbackTitle));
            }
            else
            {
                var conv = await repo.GetConversationByIdAsync(tenantId, convId, ct);
                if (conv == null) throw new InvalidOperationException("Conversation not found.");
                conv.MarkUpdated();
                history = (await repo.GetMessagesAsync(tenantId, convId, ct)).ToList();
            }

            repo.AddMessage(new OpsMessage(Guid.CreateVersion7(), tenantId, convId, "user", userMessage));
            await repo.SaveChangesAsync(ct);
        }

        yield return new ChatStreamChunkDto { Type = "conversation_id", Content = convId.ToString() };

        List<ChatMessage> messages = BuildInitialMessages(tenantId, history, userMessage);
        var options = BuildChatOptions();
        var chatClient = _clientFactory.CreateClient(thinkingEnabled: true, reasoningEffort: "xhigh");

        int maxIterations = 3;
        int iterations = 0;
        string accumulatedAssistantText = "";
        string? finalToolStatus = null;
        string? finalProposedActionJson = null;

        try
        {
            while (iterations < maxIterations)
            {
                var toolCallAccumulators = new Dictionary<int, ToolCallAccumulator>();
                var hasToolCalls = false;
                ChatTokenUsage? finalUsage = null;
                IAsyncEnumerator<StreamingChatCompletionUpdate>? enumerator = null;
                Exception? streamError = null;

                try
                {
                    var streamingResult = chatClient.CompleteChatStreamingAsync(messages, options, ct);
                    enumerator = streamingResult.GetAsyncEnumerator(ct);
                }
                catch (Exception ex)
                {
                    streamError = ex;
                }

                if (streamError != null)
                {
                    yield return new ChatStreamChunkDto { Type = "text", Content = $"\n⚠️ **LLM Request Error:**\n`{streamError.Message}`" };
                    yield break;
                }

                string currentIterationText = "";

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
                            var text = update.ContentUpdate[0].Text;
                            currentIterationText += text;
                            accumulatedAssistantText += text;
                            yield return new ChatStreamChunkDto { Type = "text", Content = text };
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
                    yield return new ChatStreamChunkDto { Type = "text", Content = $"\n⚠️ **LLM Streaming Error:**\n`{streamError.Message}`" };
                    yield break;
                }

                TrackAndLogCost(finalUsage, tenantId);

                if (hasToolCalls)
                {
                    var toolCalls = toolCallAccumulators.Values
                        .Select(a =>
                        {
                            var jsonArgs = Encoding.UTF8.GetString(a.ArgumentsStream.ToArray());
                            var cleanArgs = string.IsNullOrWhiteSpace(jsonArgs) ? "{}" : jsonArgs;
                            return ChatToolCall.CreateFunctionToolCall(a.Id, a.Name, BinaryData.FromString(cleanArgs));
                        })
                        .ToList();

                    foreach (var acc in toolCallAccumulators.Values) acc.Dispose();

                    var assistantParts = new List<ChatMessageContentPart>();
                    if (!string.IsNullOrEmpty(currentIterationText))
                    {
                        assistantParts.Add(ChatMessageContentPart.CreateTextPart(currentIterationText));
                    }

                    var assistantMsg = new AssistantChatMessage(toolCalls);
                    messages.Add(assistantMsg);

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
                            finalProposedActionJson = JsonSerializer.Serialize(proposedAction);
                            yield return new ChatStreamChunkDto { Type = "proposed_action", Proposed_action = proposedAction };
                            yield break;
                        }

                        finalToolStatus = $"Executed {definition.Name}";
                        yield return new ChatStreamChunkDto { Type = "tool_status", Tool_name = definition.Name };

                        var resultJson = await ExecuteReadToolAsync(definition, toolCall.FunctionArguments.ToString(), tenantId, ct);
                        messages.Add(new ToolChatMessage(toolCall.Id, resultJson));
                    }

                    iterations++;

                    if (iterations >= 2)
                    {
                        messages.Add(new SystemChatMessage("SYSTEM ALARM: You have executed the necessary tools and received the data. Output a final text summary immediately. DO NOT execute any more tools."));
                        options.ToolChoice = ChatToolChoice.CreateNoneChoice(); 
                    }
                }
                else
                {
                    yield break;
                }
            }

            yield return new ChatStreamChunkDto { Type = "text", Content = "\nExecution limit reached. Please refine your request." };
        }
        finally
        {
            try
            {
                using var finishScope = _scopeFactory.CreateScope();
                var repo = finishScope.ServiceProvider.GetRequiredService<IOpsRepository>();

                var assistantMsg = new OpsMessage(
                    Guid.CreateVersion7(),
                    tenantId,
                    convId,
                    "assistant",
                    string.IsNullOrWhiteSpace(accumulatedAssistantText) ? "[Tool Execution]" : accumulatedAssistantText,
                    finalToolStatus,
                    finalProposedActionJson);

                repo.AddMessage(assistantMsg);

                if (isNew)
                {
                    var titleGen = finishScope.ServiceProvider.GetRequiredService<ILlmTitleGenerator>();
                    var newTitle = await titleGen.GenerateAsync(userMessage);
                    var conv = await repo.GetConversationByIdAsync(tenantId, convId, default);
                    if (conv != null)
                    {
                        conv.UpdateTitle(newTitle);
                    }
                }

                await repo.SaveChangesAsync(default);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist final OpsMessage to database.");
            }
        }
    }

    private Guid GetValidatedTenantId()
    {
        var tenantId = _executionContext.TenantId;
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Active workspace context required. Please select a valid target workspace in the UI.");
        }
        return tenantId;
    }

    private void TrackAndLogCost(ChatTokenUsage? usage, Guid tenantId)
    {
        if (usage == null) return;
        _logger.LogInformation("FinOps [Tenant: {TenantId}] - Input: {Input}, Output: {Output}",
            tenantId, usage.InputTokenCount, usage.OutputTokenCount);
    }

    private List<ChatMessage> BuildInitialMessages(Guid tenantId, IEnumerable<OpsMessage> history, string currentMessage)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(
                $"You are Lazuar Ops, a highly capable internal operations agent. " +
                $"The current Target OrganizationId is {tenantId}. " +
                $"**CRITICAL RULE 1**: You must ALWAYS use search tools to find exact GUID identifiers before executing any write commands. NEVER guess or hallucinate a Guid! " +
                $"**CRITICAL RULE 2**: You MUST use the native tool calling API. NEVER output raw JSON or fake system messages (like '[I proposed...]') in your text response. " +
                $"**CRITICAL RULE 3**: NEVER guess or manually construct URLs. You MUST ALWAYS use the appropriate tool to retrieve exact URLs. " +
                $"**CRITICAL RULE 4**: When you need to collect multiple fields of data from the user, output a markdown code block with the language `form`. Inside it, list the exact field names you need, one per line, ending with a colon. Put default data after the colon if you have it. " +
                $"**CRITICAL RULE 5**: When executing bulk actions (Broadcasts) or financial lookups (Global Ledger), rely on the dedicated batch tools. Never attempt to loop through individual subscriber tools to send bulk messages, as this will violate system timeout boundaries."
            )
        };

        foreach (var msg in history)
        {
            if (string.IsNullOrWhiteSpace(msg.Content) && string.IsNullOrWhiteSpace(msg.ProposedActionJson))
                continue;

            var content = msg.Content;

            if (msg.Role == "user")
            {
                messages.Add(new UserChatMessage(content));
            }
            else if (msg.Role == "assistant")
            {
                messages.Add(new AssistantChatMessage(content));
                if (!string.IsNullOrEmpty(msg.ProposedActionJson))
                {
                    messages.Add(new SystemChatMessage($"[System Log: You invoked a tool with payload: {msg.ProposedActionJson}]"));
                }
            }
            else if (msg.Role == "system")
            {
                messages.Add(new SystemChatMessage(content));
            }
        }

        messages.Add(new UserChatMessage(currentMessage));
        return messages;
    }

    private ChatCompletionOptions BuildChatOptions()
    {
        var options = new ChatCompletionOptions();
        var tools = _toolRegistry.GetAvailableTools("SUPER_ADMIN").ToList();
        if (tools.Any()) foreach (var tool in tools) options.Tools.Add(tool.ChatTool);
        return options;
    }

    private ProposedActionDto BuildProposedAction(AgentToolDefinition definition, string arguments)
    {
        object payload;
        try
        {
            var cleanArgs = string.IsNullOrWhiteSpace(arguments) ? "{}" : arguments;
            payload = JsonSerializer.Deserialize<object>(cleanArgs) ?? new object();
        }
        catch (JsonException)
        {
            payload = new { _error = "The AI generated invalid parameters.", _raw_output = arguments };
        }

        var name = definition.Name.Replace("Command", "");
        var intent = string.Concat(name.Select(x => char.IsUpper(x) ? " " + x : x.ToString())).TrimStart();

        return new ProposedActionDto
        {
            Idempotency_key = Guid.CreateVersion7().ToString(),
            Tool_name = definition.Name,
            Intent_title = intent,
            Severity = definition.Severity,
            Human_readable_summary = $"Proposing to execute {intent}.",
            Command_payload = payload
        };
    }

    private async Task<string> ExecuteReadToolAsync(AgentToolDefinition definition, string arguments, Guid tenantId, CancellationToken ct)
    {
        try
        {
            var cleanArgs = string.IsNullOrWhiteSpace(arguments) || arguments.Trim() == "{}"
                ? "{}"
                : arguments;

            JsonNode jsonNode;
            try
            {
                var jsonObject = JsonSerializer.Deserialize<JsonElement>(cleanArgs);
                jsonNode = JsonNode.Parse(jsonObject.GetRawText()) as JsonObject ?? new JsonObject();
            }
            catch (JsonException)
            {
                jsonNode = new JsonObject();
            }

            jsonNode["OrganizationId"] = tenantId.ToString();

            var deserializeOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var args = jsonNode.Deserialize(definition.RequestType, deserializeOptions);

            if (args == null)
            {
                return "Error: Failed to deserialize arguments into command.";
            }

            var result = await _mediator.Send(args, ct);
            return JsonSerializer.Serialize(result);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private class ToolCallAccumulator : IDisposable
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public MemoryStream ArgumentsStream { get; } = new MemoryStream();
        public void Dispose() => ArgumentsStream.Dispose();
    }
}
