using System;

namespace BuildingBlocks.Application;

/// <summary>
/// Tags a MediatR IQuery or ICommand to be exposed as an autonomous or supervised tool for the AI Agent.
/// The ToolRegistry scans for this attribute to generate LLM-compatible JSON Schemas on startup.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public sealed class AgentToolAttribute : Attribute
{
    public string Description { get; }
    public string Severity { get; }
    public string[] AllowedRoles { get; }

    public AgentToolAttribute(string description, string severity = "low", params string[] allowedRoles)
    {
        Description = description;
        Severity = severity;
        AllowedRoles = allowedRoles;
    }
}
