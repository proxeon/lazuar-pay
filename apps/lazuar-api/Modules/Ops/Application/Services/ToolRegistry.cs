using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BuildingBlocks.Application;

namespace Modules.Ops.Application.Services;

public class ToolRegistry : IToolRegistry
{
    private readonly ConcurrentDictionary<string, AgentToolDefinition> _tools = new();

    public ToolRegistry()
    {
        DiscoverTools();
    }

    private void DiscoverTools()
    {
        var queryTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.GetCustomAttribute<AgentToolAttribute>() != null && !t.IsAbstract && !t.IsInterface);

        foreach (var type in queryTypes)
        {
            var attribute = type.GetCustomAttribute<AgentToolAttribute>()!;
            var schema = GenerateJsonSchema(type);
            
            bool isWriteCommand = type.GetInterfaces().Any(i => i == typeof(ICommand) || (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>)));

            _tools.TryAdd(type.Name, new AgentToolDefinition(
                type.Name,
                attribute.Description,
                attribute.Severity,
                schema.ToJsonString(),
                type,
                isWriteCommand
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
            var propType = prop.PropertyType;

            // Security: Prevent LLM from hallucinating protected context boundaries
            if (propName == "OrganizationId" || propName == "Id" || propName == "RecordedBy") continue;

            var typeSchema = new JsonObject();

            if (propType == typeof(string) || propType == typeof(Guid) || propType == typeof(Guid?))
            {
                typeSchema["type"] = "string";
            }
            else if (propType == typeof(int) || propType == typeof(int?))
            {
                typeSchema["type"] = "integer";
            }
            else if (propType == typeof(decimal) || propType == typeof(double) || propType == typeof(float))
            {
                typeSchema["type"] = "number";
            }
            else if (propType == typeof(bool) || propType == typeof(bool?))
            {
                typeSchema["type"] = "boolean";
            }
            else
            {
                typeSchema["type"] = "string";
            }

            properties[propName] = typeSchema;

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
