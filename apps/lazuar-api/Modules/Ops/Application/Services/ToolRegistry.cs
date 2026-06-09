using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using BuildingBlocks.Application;
using OpenAI.Chat;

namespace Modules.Ops.Application.Services;

public class ToolRegistry : IToolRegistry
{
    private readonly ConcurrentDictionary<string, AgentToolDefinition> _tools = new(StringComparer.OrdinalIgnoreCase);

    public ToolRegistry()
    {
        DiscoverTools();
    }

    /// <summary>
    /// Uses reflection to scan all loaded assemblies for MediatR records tagged with [AgentTool].
    /// Automatically maps C# properties to JSON Schema definitions and caches the official OpenAI ChatTool object.
    /// </summary>
    private void DiscoverTools()
    {
        var queryTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.GetCustomAttribute<AgentToolAttribute>() != null && !t.IsAbstract && !t.IsInterface);

        foreach (var type in queryTypes)
        {
            var attribute = type.GetCustomAttribute<AgentToolAttribute>()!;
            var schema = GenerateJsonSchema(type);
            
            // Detect if this is a Write operation (Command) or Read operation (Query)
            bool isWriteCommand = type.GetInterfaces().Any(i => i == typeof(ICommand) || (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>)));

            var chatTool = ChatTool.CreateFunctionTool(
                functionName: type.Name,
                functionDescription: attribute.Description,
                functionParameters: BinaryData.FromString(schema.ToJsonString())
            );

            _tools.TryAdd(type.Name, new AgentToolDefinition(
                type.Name,
                attribute.Description,
                attribute.Severity,
                type,
                isWriteCommand,
                chatTool
            ));
        }
    }

    public IEnumerable<AgentToolDefinition> GetAvailableTools(string userRole)
    {
        return _tools.Values.Where(t => 
        {
            var attr = t.RequestType.GetCustomAttribute<AgentToolAttribute>();
            return attr != null && (attr.AllowedRoles.Length == 0 || attr.AllowedRoles.Contains(userRole, StringComparer.OrdinalIgnoreCase));
        });
    }

    public AgentToolDefinition? GetToolDefinition(string toolName)
    {
        _tools.TryGetValue(toolName, out var definition);
        return definition;
    }

    private JsonObject GenerateJsonSchema(Type type)
    {
        var schema = new JsonObject { ["type"] = "object" };
        var properties = new JsonObject();
        var required = new JsonArray();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var propName = prop.Name;
            
            // Security: Prevent LLM from hallucinating protected context boundaries
            // These properties are populated programmatically by the execution middleware
            if (propName == "OrganizationId" || propName == "Id" || propName == "RecordedBy") continue;

            var propType = prop.PropertyType;
            var underlyingType = Nullable.GetUnderlyingType(propType) ?? propType;
            var typeSchema = new JsonObject();

            if (underlyingType == typeof(string) || underlyingType == typeof(Guid))
            {
                typeSchema["type"] = "string";
            }
            else if (underlyingType == typeof(int) || underlyingType == typeof(long))
            {
                typeSchema["type"] = "integer";
            }
            else if (underlyingType == typeof(decimal) || underlyingType == typeof(double) || underlyingType == typeof(float))
            {
                typeSchema["type"] = "number";
            }
            else if (underlyingType == typeof(bool))
            {
                typeSchema["type"] = "boolean";
            }
            else
            {
                typeSchema["type"] = "string";
            }

            properties[propName] = typeSchema;

            // Mark property as required if it is a non-nullable value type or a required string
            if (Nullable.GetUnderlyingType(propType) == null && propType.IsValueType || propType == typeof(string))
            {
                required.Add(propName);
            }
        }

        schema["properties"] = properties;
        if (required.Count > 0) schema["required"] = required;
        schema["additionalProperties"] = false;

        return schema;
    }
}
