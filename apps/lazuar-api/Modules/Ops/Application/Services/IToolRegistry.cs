using System;
using System.Collections.Generic;

namespace Modules.Ops.Application.Services;

public record AgentToolDefinition(string Name, string Description, string JsonSchema, Type RequestType);

public interface IToolRegistry
{
    IEnumerable<AgentToolDefinition> GetAvailableTools(string userRole);
    Type? GetToolRequestType(string toolName);
}
