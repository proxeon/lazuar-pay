using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
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
        var queryTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => 
            {
                try { return a.GetTypes(); }
                catch (ReflectionTypeLoadException e) { return e.Types; }
                catch { return Type.EmptyTypes; }
            })
            .OfType<Type>()
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
        return (JsonObject)GetSchemaForType(type);
    }

    private JsonNode GetSchemaForType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
        var schema = new JsonObject();

        if (underlyingType == typeof(int) || underlyingType == typeof(long))
        {
            schema["type"] = "integer";
            return schema;
        }
        
        if (underlyingType == typeof(decimal) || underlyingType == typeof(double) || underlyingType == typeof(float))
        {
            schema["type"] = "number";
            return schema;
        }
        
        if (underlyingType == typeof(bool))
        {
            schema["type"] = "boolean";
            return schema;
        }
        
        if (underlyingType == typeof(string) || underlyingType == typeof(Guid) || underlyingType == typeof(DateTime) || underlyingType == typeof(DateTimeOffset))
        {
            schema["type"] = "string";
            return schema;
        }

        if (typeof(System.Collections.IEnumerable).IsAssignableFrom(underlyingType) && underlyingType != typeof(string))
        {
            schema["type"] = "array";
            var elementType = GetElementType(underlyingType);
            
            schema["items"] = elementType != null 
                ? GetSchemaForType(elementType) 
                : new JsonObject { ["type"] = "string" };
                
            return schema;
        }

        schema["type"] = "object";
        var properties = new JsonObject();
        var required = new JsonArray();

        foreach (var prop in underlyingType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var jsonAttr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
            var propName = jsonAttr?.Name ?? prop.Name;

            // We hide these fields from the AI so it doesn't try to hallucinate them
            if (propName == "OrganizationId" || propName == "Id" || propName == "RecordedBy") continue;

            properties[propName] = GetSchemaForType(prop.PropertyType);

            var isNullableValueType = Nullable.GetUnderlyingType(prop.PropertyType) != null;
            if (!isNullableValueType && (prop.PropertyType.IsValueType || prop.PropertyType == typeof(string)))
            {
                required.Add(propName);
            }
        }

        // FIX: Google Gemini strictly rejects tools with 0 properties. 
        // Since we hide `OrganizationId`, many of our tools become empty. 
        // We inject a dummy variable here to keep the schema valid for Gemini.
        if (properties.Count == 0)
        {
            properties["_meta"] = new JsonObject { ["type"] = "string", ["description"] = "Optional metadata context. Leave empty." };
        }

        schema["properties"] = properties;
        if (required.Count > 0) schema["required"] = required;

        return schema;
    }

    private Type? GetElementType(Type type)
    {
        if (type.IsArray) return type.GetElementType();
        
        var enumerableType = type.GetInterfaces()
            .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        
        if (enumerableType != null) return enumerableType.GetGenericArguments()[0];
        
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            return type.GetGenericArguments()[0];
            
        return null;
    }
}
