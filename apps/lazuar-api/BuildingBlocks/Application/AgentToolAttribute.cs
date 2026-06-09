using System;

namespace BuildingBlocks.Application;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public sealed class AgentToolAttribute : Attribute
{
    public string Description { get; }
    public string[] AllowedRoles { get; }

    public AgentToolAttribute(string description, params string[] allowedRoles)
    {
        Description = description;
        AllowedRoles = allowedRoles;
    }
}
