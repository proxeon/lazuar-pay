// apps/lazuar-api/Modules/Ops/Infrastructure/Services/LlmOrchestratorService.cs
// Stream loop + BinaryData / tool-arg accumulation history: see LlmOrchestratorService.Stream.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Ops.Contracts;
using Modules.Ops.Application.Llm;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modules.One.Contracts;
using Modules.Ops.Application;
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
