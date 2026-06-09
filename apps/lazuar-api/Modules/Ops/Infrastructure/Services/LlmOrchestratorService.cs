using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
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

    public async Task<string> ProcessChatAsync(string userMessage, CancellationToken ct = default)
    {
        var tools = _toolRegistry.GetAvailableTools(_executionContext.UserRole).ToList();
        
        var chatTools = tools.Select(t => ChatTool.CreateFunctionTool(
            functionName: t.Name,
            functionDescription: t.Description,
            functionParameters: BinaryData.FromString(t.JsonSchema)
        )).ToList();

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage($"You are Lazuar Ops, a highly capable internal agent. You strictly answer based on the tools available. The current OrganizationId is {_executionContext.TenantId}."),
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
                    var requestType = _toolRegistry.GetToolRequestType(toolCall.FunctionName);
                    if (requestType == null) continue;

                    var args = JsonSerializer.Deserialize(toolCall.FunctionArguments.ToString(), requestType, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    try
                    {
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
                return completion.Value.Content[0].Text;
            }
        }

        return "I needed to gather too much information and hit my execution limit. Could you be more specific?";
    }
}
