using System.Linq;
using FluentAssertions;
using Modules.Ops.Application.Commands;
using Modules.Ops.Application.Services;
using Modules.Ops.Contracts;
using NUnit.Framework;

namespace Modules.Ops.Tests.Services;

public class ToolRegistryTests
{
    [Test]
    public void DiscoverTools_Finds_Types_Annotated_With_OpsContracts_AgentToolAttribute()
    {
        // Ensure Ops.Application (and its [AgentTool] types) are loaded into the AppDomain.
        _ = typeof(RequestFormInputCommand);
        _ = typeof(AgentToolAttribute);

        var registry = new ToolRegistry();
        var tool = registry.GetToolDefinition(nameof(RequestFormInputCommand));

        tool.Should().NotBeNull();
        tool!.Name.Should().Be(nameof(RequestFormInputCommand));
        tool.RequestType.Should().Be(typeof(RequestFormInputCommand));
        tool.IsWriteCommand.Should().BeTrue();

        var available = registry.GetAvailableTools("ADMIN", activeAppIds: Enumerable.Empty<string>()).ToList();
        available.Should().Contain(t => t.Name == nameof(RequestFormInputCommand));
    }
}
