// apps/lazuar-api/Modules/Ops/Application/Services/IToolRegistry.cs
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
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
    IEnumerable<AgentToolDefinition> GetAvailableTools(string userRole, IEnumerable<string> activeAppIds);
    AgentToolDefinition? GetToolDefinition(string toolName);
    JsonObject? GetSchemaForTool(string toolName);
}
