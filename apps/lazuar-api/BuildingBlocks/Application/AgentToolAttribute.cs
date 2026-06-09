using System;

namespace BuildingBlocks.Application;

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
