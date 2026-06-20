// apps/lazuar-api/Modules/Ops/Application/Services/ToolRegistry.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text.Json;
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

    public IEnumerable<AgentToolDefinition> GetAvailableTools(string userRole, IEnumerable<string> activeAppIds)
    {
        var activeAppsSet = new HashSet<string>(activeAppIds, StringComparer.OrdinalIgnoreCase);

        return _tools.Values.Where(t =>
        {
            var attr = t.RequestType.GetCustomAttribute<AgentToolAttribute>();
            if (attr == null) return false;

            bool roleMatches = attr.AllowedRoles.Length == 0 || attr.AllowedRoles.Contains(userRole, StringComparer.OrdinalIgnoreCase);
            bool appMatches = string.Equals(attr.RequiredAppId, "CORE", StringComparison.OrdinalIgnoreCase) || activeAppsSet.Contains(attr.RequiredAppId);

            return roleMatches && appMatches;
        });
    }

    public AgentToolDefinition? GetToolDefinition(string toolName)
    {
        _tools.TryGetValue(toolName, out var definition);
        return definition;
    }

    public JsonObject? GetSchemaForTool(string toolName)
    {
        if (_tools.TryGetValue(toolName, out var definition))
        {
            return GenerateJsonSchema(definition.RequestType);
        }
        return null;
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

        if (underlyingType == typeof(string) || underlyingType == typeof(Guid))
        {
            schema["type"] = "string";
            return schema;
        }

        if (underlyingType == typeof(DateTime) || underlyingType == typeof(DateTimeOffset))
        {
            schema["type"] = "string";
            schema["format"] = "date-time";
            return schema;
        }

        if (underlyingType.IsEnum)
        {
            schema["type"] = "string";
            var enumArray = new JsonArray();
            foreach (var name in Enum.GetNames(underlyingType))
            {
                enumArray.Add(name);
            }
            schema["enum"] = enumArray;
            return schema;
        }

        if (underlyingType == typeof(object) || 
            typeof(JsonNode).IsAssignableFrom(underlyingType) || 
            typeof(JsonDocument).IsAssignableFrom(underlyingType) || 
            typeof(JsonElement).IsAssignableFrom(underlyingType) ||
            (underlyingType.IsGenericType && underlyingType.GetGenericTypeDefinition() == typeof(Dictionary<,>)))
        {
            schema["type"] = "object";
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

            if (propName == "OrganizationId" || propName == "Id" || propName == "RecordedBy") continue;

            var propSchema = GetSchemaForType(prop.PropertyType);

            var descriptionAttr = prop.GetCustomAttribute<DescriptionAttribute>();
            if (descriptionAttr != null && propSchema is JsonObject propSchemaObj)
            {
                propSchemaObj["description"] = descriptionAttr.Description;
            }

            properties[propName] = propSchema;

            var isNullableValueType = Nullable.GetUnderlyingType(prop.PropertyType) != null;
            if (!isNullableValueType && (prop.PropertyType.IsValueType || prop.PropertyType == typeof(string)))
            {
                required.Add(propName);
            }
        }

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
