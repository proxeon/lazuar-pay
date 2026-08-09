using System;

namespace Modules.Ops.Contracts;

/// <summary>
/// Marks a MediatR command/query as an Ops agent tool for <c>ToolRegistry</c> discovery.
/// Cross-module extension point — live in Ops.Contracts so other modules can annotate without
/// referencing Ops.Application/Infrastructure.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public sealed class AgentToolAttribute : Attribute
{
    public string Description { get; }
    public string RequiredAppId { get; }
    public string Severity { get; }
    public string[] AllowedRoles { get; }

    public AgentToolAttribute(string description, string requiredAppId, string severity = "low", params string[] allowedRoles)
    {
        Description = description;
        RequiredAppId = requiredAppId;
        Severity = severity;
        AllowedRoles = allowedRoles;
    }
}
