using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Application.Llm;
using Lazuar.ApiTypes;
using MediatR;
using Modules.Ops.Application.Services;
using OpenAI.Chat;

namespace Modules.Ops.Infrastructure.Services;

public class LlmOrchestratorService : ILlmOrchestratorService
{
    private readonly IChatClientFactory _clientFactory;
    private readonly IToolRegistry _toolRegistry;
    private readonly IMediator _mediator;
    private readonly IExecutionContextAccessor _executionContext;

    public LlmOrchestratorService(
        IChatClientFactory clientFactory,
        IToolRegistry toolRegistry,
        IMediator mediator,
        IExecutionContextAccessor executionContext)
    {
        _clientFactory = clientFactory;
        _toolRegistry = toolRegistry;
        _mediator = mediator;
        _executionContext = executionContext;
    }

    public async Task<ChatResponseDto> ProcessChatAsync(string userMessage, CancellationToken ct = default)
    {
        var messages = BuildInitialMessages(userMessage);
        var options = BuildChatOptions();
        var chatClient = _clientFactory.CreateClient();

        int maxIterations = 3;
        int iterations = 0;

        while (iterations < maxIterations)
        {
            var completion = await chatClient.CompleteChatAsync(messages, options, ct);

            if (completion.Value.FinishReason == ChatFinishReason.ToolCalls)
            {
                messages.Add(new AssistantChatMessage(completion.Value));

                foreach (var toolCall in completion.Value.ToolCalls)
                {
                    var definition = _toolRegistry.GetToolDefinition(toolCall.FunctionName);
                    if (definition == null)
                    {
                        messages.Add(new ToolChatMessage(toolCall.Id, "Error: Tool not found."));
                        continue;
                    }

                    if (definition.IsWriteCommand)
                    {
                        return new ChatResponseDto 
                        { 
                            Message = "I have staged this action for your review.", 
                            Proposed_action = BuildProposedAction(definition, toolCall.FunctionArguments.ToString()) 
                        };
                    }

                    var resultJson = await ExecuteReadToolAsync(definition, toolCall.FunctionArguments.ToString(), ct);
                    messages.Add(new ToolChatMessage(toolCall.Id, resultJson));
                }
                
                iterations++;
            }
            else
            {
                return new ChatResponseDto { Message = completion.Value.Content[0].Text };
            }
        }

        return new ChatResponseDto { Message = "Execution limit reached. Please be more specific." };
    }

    public async IAsyncEnumerable<ChatStreamChunkDto> ProcessChatStreamAsync(string userMessage, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var messages = BuildInitialMessages(userMessage);
        var options = BuildChatOptions();
        var chatClient = _clientFactory.CreateClient();

        int maxIterations = 3;
        int iterations = 0;

        while (iterations < maxIterations)
        {
            var streamingResult = chatClient.CompleteChatStreamingAsync(messages, options, ct);
            var toolCallAccumulators = new Dictionary<int, ToolCallAccumulator>();
            var hasToolCalls = false;
            var textAppended = false;

            await foreach (var update in streamingResult.WithCancellation(ct))
            {
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
                    if (toolUpdate.FunctionArgumentsUpdate != null) acc.Arguments += toolUpdate.FunctionArgumentsUpdate;
                }
            }

            if (hasToolCalls)
            {
                var toolCalls = toolCallAccumulators.Values
                    .Select(a => ChatToolCall.CreateFunctionToolCall(a.Id, a.Name, BinaryData.FromString(a.Arguments)))
                    .ToList();

                messages.Add(new AssistantChatMessage(toolCalls));

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

    private List<ChatMessage> BuildInitialMessages(string userMessage)
    {
        return new List<ChatMessage>
        {
            new SystemChatMessage(
                $"You are Lazuar Ops, a highly capable internal operations agent. " +
                $"The current OrganizationId is {_executionContext.TenantId}. " +
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
        var tools = _toolRegistry.GetAvailableTools(_executionContext.UserRole);
        foreach (var tool in tools) options.Tools.Add(tool.ChatTool);
        return options;
    }

    private ProposedActionDto BuildProposedAction(AgentToolDefinition definition, string arguments)
    {
        return new ProposedActionDto
        {
            Idempotency_key = Guid.CreateVersion7().ToString(),
            Tool_name = definition.Name,
            Intent_title = FormatIntentTitle(definition.Name),
            Severity = definition.Severity,
            Human_readable_summary = $"Proposing to execute {FormatIntentTitle(definition.Name)}.",
            Command_payload = JsonSerializer.Deserialize<object>(arguments)
        };
    }

    private async Task<string> ExecuteReadToolAsync(AgentToolDefinition definition, string arguments, CancellationToken ct)
    {
        try
        {
            var jsonNode = JsonSerializer.SerializeToNode(JsonSerializer.Deserialize<object>(arguments)) as JsonObject ?? new JsonObject();
            jsonNode["OrganizationId"] = _executionContext.TenantId;

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

    private class ToolCallAccumulator
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Arguments { get; set; } = "";
    }
}
