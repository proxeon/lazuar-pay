using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.Extensions.Configuration;
using Modules.Ops.Application.Services;
using OpenAI.Chat;

namespace Modules.Ops.Infrastructure.Services;

public class LlmOrchestratorService : ILlmOrchestratorService
{
    private readonly ChatClient _chatClient;
    private readonly IToolRegistry _toolRegistry;
    private readonly IMediator _mediator;
    private readonly IExecutionContextAccessor _executionContext;

    public LlmOrchestratorService(
        IConfiguration configuration,
        IToolRegistry toolRegistry,
        IMediator mediator,
        IExecutionContextAccessor executionContext)
    {
        _toolRegistry = toolRegistry;
        _mediator = mediator;
        _executionContext = executionContext;

        var apiKey = configuration["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI API Key is missing.");
        _chatClient = new ChatClient("gpt-4o-mini", apiKey);
    }

    public async Task<ChatResponseDto> ProcessChatAsync(string userMessage, CancellationToken ct = default)
    {
        var tools = _toolRegistry.GetAvailableTools(_executionContext.UserRole).ToList();
        
        var chatTools = tools.Select(t => ChatTool.CreateFunctionTool(
            functionName: t.Name,
            functionDescription: t.Description,
            functionParameters: BinaryData.FromString(t.JsonSchema)
        )).ToList();

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage($"You are Lazuar Ops, a highly capable internal agent. The current OrganizationId is {_executionContext.TenantId}. " +
                                  $"If asked to perform a write action, strictly call the relevant tool. Do not chain multiple write tools in a single turn."),
            new UserChatMessage(userMessage)
        };

        var options = new ChatCompletionOptions();
        foreach (var tool in chatTools) options.Tools.Add(tool);

        int maxIterations = 3;
        int iterations = 0;

        while (iterations < maxIterations)
        {
            var completion = await _chatClient.CompleteChatAsync(messages, options, ct);

            if (completion.Value.FinishReason == ChatFinishReason.ToolCalls)
            {
                messages.Add(new AssistantChatMessage(completion.Value));

                foreach (var toolCall in completion.Value.ToolCalls)
                {
                    var definition = _toolRegistry.GetToolDefinition(toolCall.FunctionName);
                    if (definition == null) continue;

                    // Intercept Write Commands: Supervised Autonomy Pattern
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

                    // Autonomous Reads
                    var args = JsonSerializer.Deserialize(toolCall.FunctionArguments.ToString(), definition.RequestType, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    try
                    {
                        var jsonNode = JsonSerializer.SerializeToNode(args) as System.Text.Json.Nodes.JsonObject;
                        if (jsonNode != null)
                        {
                            jsonNode["OrganizationId"] = _executionContext.TenantId;
                            args = jsonNode.Deserialize(definition.RequestType);
                        }

                        var result = await _mediator.Send(args!, ct);
                        messages.Add(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result)));
                    }
                    catch (Exception ex)
                    {
                        messages.Add(new ToolChatMessage(toolCall.Id, $"Error executing tool: {ex.Message}"));
                    }
                }
                
                iterations++;
            }
            else
            {
                return new ChatResponseDto { Message = completion.Value.Content[0].Text };
            }
        }

        return new ChatResponseDto { Message = "I needed to gather too much information and hit my execution limit. Could you be more specific?" };
    }

    private static string FormatIntentTitle(string commandName)
    {
        var name = commandName.Replace("Command", "");
        return string.Concat(name.Select(x => Char.IsUpper(x) ? " " + x : x.ToString())).TrimStart();
    }
}
