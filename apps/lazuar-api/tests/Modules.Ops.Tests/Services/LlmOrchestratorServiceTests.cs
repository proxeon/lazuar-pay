using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Ops.Contracts;
using Modules.Ops.Application.Llm;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modules.One.Contracts;
using Modules.Ops.Application.Services;
using Modules.Ops.Infrastructure.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;

namespace Modules.Ops.Tests.Services;

public class LlmOrchestratorServiceTests
{
    private IChatClientFactory _clientFactory = null!;
    private IToolRegistry _toolRegistry = null!;
    private IMediator _mediator = null!;
    private IExecutionContextAccessor _executionContext = null!;
    private IServiceScopeFactory _scopeFactory = null!;
    private IOneQueryService _oneQueryService = null!;
    private IEnumerable<IAgentPromptProvider> _promptProviders = null!;
    private IConfiguration _configuration = null!;
    private ILogger<LlmOrchestratorService> _logger = null!;
    private LlmOrchestratorService _service = null!;
    private MethodInfo _executeReadToolMethod = null!;

    // Dummy Query matching the behavior of GetPlanLinkAgentQuery
    public class DummyReadQuery : IQuery<string>
    {
        public Guid OrganizationId { get; set; }
        public string? TargetSlug { get; set; }
    }

    [SetUp]
    public void Setup()
    {
        _clientFactory = Substitute.For<IChatClientFactory>();
        _toolRegistry = Substitute.For<IToolRegistry>();
        _mediator = Substitute.For<IMediator>();
        _executionContext = Substitute.For<IExecutionContextAccessor>();
        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _oneQueryService = Substitute.For<IOneQueryService>();
        _promptProviders = Array.Empty<IAgentPromptProvider>();
        // Real config: NSubstitute returns "" for missing keys, which breaks GetValue<int>(..., default).
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:MaxToolIterations"] = "7"
            })
            .Build();
        _logger = Substitute.For<ILogger<LlmOrchestratorService>>();

        // Inject the newly required dependencies into the service constructor
        _service = new LlmOrchestratorService(
            _clientFactory, 
            _toolRegistry, 
            _mediator, 
            _executionContext, 
            _scopeFactory, 
            _oneQueryService,
            _promptProviders,
            _configuration,
            _logger);

        _executeReadToolMethod = typeof(LlmOrchestratorService).GetMethod(
            "ExecuteReadToolAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
    }

    private async Task<string> InvokeExecuteReadToolAsync(AgentToolDefinition definition, string arguments, Guid tenantId)
    {
        var task = (Task<string>)_executeReadToolMethod.Invoke(
            _service, new object[] { definition, arguments, tenantId, CancellationToken.None })!;
        
        return await task;
    }

    [Test]
    public async Task ExecuteReadToolAsync_WithEmptyString_SafelyInjectsTenantId()
    {
        var tenantId = Guid.NewGuid();
        var toolDef = new AgentToolDefinition("DummyRead", "Desc", "low", typeof(DummyReadQuery), false, null!);

        // Mock the object overload specifically
        _mediator.Send(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns("SuccessResponse");

        var result = await InvokeExecuteReadToolAsync(toolDef, "", tenantId);

        result.Should().Be("\"SuccessResponse\"");
        
        await _mediator.Received(1).Send(
            Arg.Is<object>(q => q is DummyReadQuery && ((DummyReadQuery)q).OrganizationId == tenantId && ((DummyReadQuery)q).TargetSlug == null), 
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteReadToolAsync_WithMalformedJson_ReturnsErrorWithoutCallingMediator()
    {
        var tenantId = Guid.NewGuid();
        var toolDef = new AgentToolDefinition("DummyRead", "Desc", "low", typeof(DummyReadQuery), false, null!);
        var malformedArgs = "{ bad_json: unquoted, [ }";

        var result = await InvokeExecuteReadToolAsync(toolDef, malformedArgs, tenantId);

        result.Should().StartWith("System Error: Tool DummyRead rejected your payload.");
        result.Should().Contain("JSON structure was invalid");

        await _mediator.DidNotReceive().Send(Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteReadToolAsync_WithValidJson_PreservesOtherPropertiesAndInjectsTenantId()
    {
        var tenantId = Guid.NewGuid();
        var toolDef = new AgentToolDefinition("DummyRead", "Desc", "low", typeof(DummyReadQuery), false, null!);
        var validArgs = "{\"TargetSlug\": \"al-quran-class\"}";

        _mediator.Send(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns("https://localhost:3021/tenant/al-quran-class");

        var result = await InvokeExecuteReadToolAsync(toolDef, validArgs, tenantId);

        result.Should().Be("\"https://localhost:3021/tenant/al-quran-class\"");
        
        await _mediator.Received(1).Send(
            Arg.Is<object>(q => q is DummyReadQuery && ((DummyReadQuery)q).OrganizationId == tenantId && ((DummyReadQuery)q).TargetSlug == "al-quran-class"), 
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteReadToolAsync_WhenMediatorThrowsException_ReturnsErrorStringGracefully()
    {
        var tenantId = Guid.NewGuid();
        var toolDef = new AgentToolDefinition("DummyRead", "Desc", "low", typeof(DummyReadQuery), false, null!);

        _mediator.Send(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Plan not found in the current workspace."));

        var result = await InvokeExecuteReadToolAsync(toolDef, "{}", tenantId);

        result.Should().Be("Error: Plan not found in the current workspace.");
    }
}
