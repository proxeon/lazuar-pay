using System;
using System.Collections.Generic;
using OpenAI.Chat;

namespace Modules.Ops.Application.Services;

public record AgentToolDefinition(
    string Name, 
    string Description, 
    string Severity, 
    Type RequestType, 
    bool IsWriteCommand, 
    ChatTool ChatTool);

public interface IToolRegistry
{
    IEnumerable<AgentToolDefinition> GetAvailableTools(string userRole);
    AgentToolDefinition? GetToolDefinition(string toolName);
}
