using System;
using System.Collections.Generic;

namespace Modules.Ops.Application.Services;

public record AgentToolDefinition(string Name, string Description, string Severity, string JsonSchema, Type RequestType, bool IsWriteCommand);

public interface IToolRegistry
{
    IEnumerable<AgentToolDefinition> GetAvailableTools(string userRole);
    AgentToolDefinition? GetToolDefinition(string toolName);
}
