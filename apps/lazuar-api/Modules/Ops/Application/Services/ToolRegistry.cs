// apps/lazuar-api/Modules/Ops/Application/Services/ToolRegistry.cs
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

    private void DiscoverTools()
    {
        // Safely scan assemblies, catching ReflectionTypeLoadExceptions thrown by dynamic/system DLLs
        var queryTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => 
            {
                try { return a.GetTypes(); }
                catch (ReflectionTypeLoadException e) { return e.Types; }
                catch { return Type.EmptyTypes; }
            })
            .OfType<Type>() // Safely filters out nulls AND casts to non-nullable Type
            .Where(t => t.GetCustomAttribute<AgentToolAttribute>() != null && !t.IsAbstract && !t.IsInterface);

        foreach (var type in queryTypes)
        {
            var attribute = type.GetCustomAttribute<AgentToolAttribute>()!;
            var schema = GenerateJsonSchema(type);
            
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
            
            if (propName == "OrganizationId" || propName == "Id" || propName == "RecordedBy") continue;

            var propType = prop.PropertyType;
            var underlyingType = Nullable.GetUnderlyingType(propType) ?? propType;
            var typeSchema = new JsonObject();

            if (underlyingType == typeof(int) || underlyingType == typeof(long)) typeSchema["type"] = "integer";
            else if (underlyingType == typeof(decimal) || underlyingType == typeof(double) || underlyingType == typeof(float)) typeSchema["type"] = "number";
            else if (underlyingType == typeof(bool)) typeSchema["type"] = "boolean";
            else typeSchema["type"] = "string";

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
