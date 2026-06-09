using System;
using System.Collections.Generic;
using System.Linq;
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
        var chatClient = _clientFactory.CreateClient();
        
        var availableTools = _toolRegistry.GetAvailableTools(_executionContext.UserRole).ToList();

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(
                $"You are Lazuar Ops, a highly capable internal operations agent. " +
                $"The current OrganizationId is {_executionContext.TenantId}. " +
                $"If the user asks you to perform a write action (create, update, delete, archive), strictly call the relevant tool. " +
                $"Do not chain multiple write tools in a single turn; stage one write action at a time. " +
                $"For read operations, you may call multiple tools to gather the necessary context before answering."
            ),
            new UserChatMessage(userMessage)
        };

        var options = new ChatCompletionOptions();
        foreach (var toolDef in availableTools)
        {
            options.Tools.Add(toolDef.ChatTool);
        }

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
                        messages.Add(new ToolChatMessage(toolCall.Id, "Error: Tool not found or not authorized."));
                        continue;
                    }

                    if (definition.IsWriteCommand)
                    {
                        var proposedAction = new ProposedActionDto
                        {
                            Idempotency_key = Guid.CreateVersion7().ToString(),
                            Tool_name = definition.Name,
                            Intent_title = FormatIntentTitle(definition.Name),
                            Severity = definition.Severity,
                            Human_readable_summary = $"Proposing to execute {FormatIntentTitle(definition.Name)}.",
                            Command_payload = JsonSerializer.Deserialize<object>(toolCall.FunctionArguments.ToString())
                        };

                        return new ChatResponseDto 
                        { 
                            Message = "I have staged this action for your review.", 
                            Proposed_action = proposedAction 
                        };
                    }

                    try
                    {
                        var jsonNode = JsonSerializer.SerializeToNode(
                            JsonSerializer.Deserialize<object>(toolCall.FunctionArguments.ToString())
                        ) as JsonObject ?? new JsonObject();

                        jsonNode["OrganizationId"] = _executionContext.TenantId;

                        var deserializeOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var args = jsonNode.Deserialize(definition.RequestType, deserializeOptions);

                        var result = await _mediator.Send(args!, ct);
                        
                        messages.Add(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result)));
                    }
                    catch (Exception ex)
                    {
                        messages.Add(new ToolChatMessage(toolCall.Id, $"Error executing read query: {ex.Message}"));
                    }
                }
                
                iterations++;
            }
            else
            {
                return new ChatResponseDto { Message = completion.Value.Content[0].Text };
            }
        }

        return new ChatResponseDto { Message = "I hit my maximum execution limit while gathering data. Please be more specific." };
    }

    private static string FormatIntentTitle(string commandName)
    {
        var name = commandName.Replace("Command", "");
        var formatted = string.Concat(name.Select(x => char.IsUpper(x) ? " " + x : x.ToString())).TrimStart();
        return formatted;
    }
}
