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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modules.One.Contracts;
using Modules.Ops.Application;
using Modules.Ops.Application.Commands;
using Modules.Ops.Application.Services;
using Modules.Ops.Domain;
using OpenAI.Chat;

namespace Modules.Ops.Infrastructure.Services;

public partial class LlmOrchestratorService : ILlmOrchestratorService
{
    private readonly IChatClientFactory _clientFactory;
    private readonly IToolRegistry _toolRegistry;
    private readonly IMediator _mediator;
    private readonly IExecutionContextAccessor _executionContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOneQueryService _oneQueryService;
    private readonly IEnumerable<IAgentPromptProvider> _promptProviders;
    private readonly ILogger<LlmOrchestratorService> _logger;
    private readonly int _maxIterations;

    public LlmOrchestratorService(
        IChatClientFactory clientFactory,
        IToolRegistry toolRegistry,
        IMediator mediator,
        IExecutionContextAccessor executionContext,
        IServiceScopeFactory scopeFactory,
        IOneQueryService oneQueryService,
        IEnumerable<IAgentPromptProvider> promptProviders,
        IConfiguration configuration,
        ILogger<LlmOrchestratorService> logger)
    {
        _clientFactory = clientFactory;
        _toolRegistry = toolRegistry;
        _mediator = mediator;
        _executionContext = executionContext;
        _scopeFactory = scopeFactory;
        _oneQueryService = oneQueryService;
        _promptProviders = promptProviders;
        _logger = logger;
        _maxIterations = configuration.GetValue<int>("Ai:MaxToolIterations", 7);
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

        var activeApps = await _oneQueryService.GetWorkspaceAppsAsync(tenantId);
        var messages = BuildInitialMessages(tenantId, history, userMessage, activeApps);
        var options = BuildChatOptions(activeApps);
        
        var chatClient = _clientFactory.CreateClient(thinkingEnabled: true, reasoningEffort: "xhigh");
        var completion = await chatClient.CompleteChatAsync(messages, options, ct);

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

        var activeApps = await _oneQueryService.GetWorkspaceAppsAsync(tenantId);
        List<ChatMessage> messages = BuildInitialMessages(tenantId, history, userMessage, activeApps);
        var options = BuildChatOptions(activeApps);
        
        var chatClient = _clientFactory.CreateClient(thinkingEnabled: true, reasoningEffort: "xhigh");

        int iterations = 0;
        string accumulatedAssistantText = "";
        
        List<string> executedTools = new();
        string? finalProposedActionJson = null;
        string? finalUiRequestJson = null;
        var toolFailureCounts = new Dictionary<string, int>();

        try
        {
            while (iterations < _maxIterations)
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
                    yield return new ChatStreamChunkDto { Type = "text", Content = $"\n\n⚠️ **LLM Request Error:**\n`{streamError.Message}`" };
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
                    yield return new ChatStreamChunkDto { Type = "text", Content = $"\n\n⚠️ **LLM Streaming Error:**\n`{streamError.Message}`" };
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

                    var assistantMsgObj = new AssistantChatMessage(toolCalls);
                    messages.Add(assistantMsgObj);

                    foreach (var toolCall in toolCalls)
                    {
                        if (toolCall.FunctionName == nameof(RequestFormInputCommand))
                        {
                            UiRequestDto? pendingUiRequest = null;
                            bool parseFailed = false;

                            try
                            {
                                var args = JsonSerializer.Deserialize<RequestFormInputCommand>(
                                    toolCall.FunctionArguments.ToString(), 
                                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                                    
                                if (args != null && !string.IsNullOrWhiteSpace(args.TargetToolName))
                                {
                                    var toolName = args.TargetToolName;
                                    var schemaNode = _toolRegistry.GetSchemaForTool(toolName);
                                    
                                    JsonNode? partialDataNode = null;
                                    if (args.PartialData != null)
                                    {
                                        if (args.PartialData is string strData && !string.IsNullOrWhiteSpace(strData))
                                        {
                                            try { partialDataNode = JsonNode.Parse(strData); } catch { }
                                        }
                                        else if (args.PartialData is JsonElement element)
                                        {
                                            try { partialDataNode = JsonNode.Parse(element.GetRawText()); } catch { }
                                        }
                                        else
                                        {
                                            try { partialDataNode = JsonSerializer.SerializeToNode(args.PartialData); } catch { }
                                        }
                                    }
                                    
                                    pendingUiRequest = new UiRequestDto
                                    {
                                        Tool_name = toolName,
                                        Schema_json = schemaNode ?? new JsonObject(),
                                        Prefill_data = partialDataNode,
                                        Is_resolved = false
                                    };
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to parse RequestFormInputCommand arguments securely.");
                                parseFailed = true;
                            }

                            if (parseFailed)
                            {
                                messages.Add(new ToolChatMessage(toolCall.Id, "System Error: The generated form request arguments were invalid JSON. Please verify your data and try calling the tool again."));
                                continue;
                            }

                            if (pendingUiRequest != null)
                            {
                                finalUiRequestJson = JsonSerializer.Serialize(pendingUiRequest);
                                yield return new ChatStreamChunkDto { Type = "ui_request", Ui_request = pendingUiRequest };
                                yield break; 
                            }
                        }

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

                        executedTools.Add(definition.Name);
                        yield return new ChatStreamChunkDto { Type = "tool_status", Tool_name = definition.Name, Executed_tools = executedTools };

                        var resultJson = await ExecuteReadToolAsync(definition, toolCall.FunctionArguments.ToString(), tenantId, ct);

                        if (resultJson.StartsWith("System Error: Tool"))
                        {
                            toolFailureCounts.TryGetValue(definition.Name, out var failCount);
                            failCount++;
                            toolFailureCounts[definition.Name] = failCount;

                            if (failCount >= 3)
                            {
                                yield return new ChatStreamChunkDto { Type = "text", Content = $"\n\n⚠️ **System Halt:** The AI agent repeatedly failed to generate a valid JSON payload for `{definition.Name}`. Execution aborted to prevent token waste." };
                                yield break;
                            }
                        }
                        else
                        {
                            toolFailureCounts[definition.Name] = 0;
                        }

                        messages.Add(new ToolChatMessage(toolCall.Id, resultJson));
                    }

                    iterations++;
                    if (iterations >= _maxIterations - 1)
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

            yield return new ChatStreamChunkDto { Type = "text", Content = "\n\nExecution limit reached. Please refine your request." };
        }
        finally
        {
            try
            {
                string? finalExecutedToolsJson = executedTools.Count > 0 ? JsonSerializer.Serialize(executedTools) : null;
                
                using var finishScope = _scopeFactory.CreateScope();
                var repo = finishScope.ServiceProvider.GetRequiredService<IOpsRepository>();
                var assistantMsg = new OpsMessage(
                    Guid.CreateVersion7(),
                    tenantId,
                    convId,
                    "assistant",
                    string.IsNullOrWhiteSpace(accumulatedAssistantText) ? "[Tool Execution]" : accumulatedAssistantText,
                    finalExecutedToolsJson,
                    finalProposedActionJson,
                    finalUiRequestJson,
                    isResolved: false);

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
}
