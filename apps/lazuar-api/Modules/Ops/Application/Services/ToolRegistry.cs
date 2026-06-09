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
            
            _tools.TryAdd(type.Name, new AgentToolDefinition(
                type.Name,
                attribute.Description,
                schema.ToJsonString(),
                type
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

    public Type? GetToolRequestType(string toolName)
    {
        _tools.TryGetValue(toolName, out var definition);
        return definition?.RequestType;
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

            var typeSchema = new JsonObject();

            if (propType == typeof(string) || propType == typeof(Guid) || propType == typeof(Guid?))
            {
                typeSchema["type"] = "string";
                if (propType == typeof(Guid) || propType == typeof(Guid?))
                {
                    typeSchema["description"] = "A valid GUID string.";
                }
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
                typeSchema["type"] = "string"; // Fallback
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
