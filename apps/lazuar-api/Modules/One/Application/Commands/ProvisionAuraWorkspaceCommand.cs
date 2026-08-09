using System;
using System.Collections.Generic;
using BuildingBlocks.Application;

namespace Modules.One.Application.Commands;

public record ProvisionAuraWorkspaceCommand(
    string AuraOrgId,
    string DisplayName,
    string? Slug,
    string? OwnerEmail,
    string? OwnerRole,
    bool IsTestMode,
    string? KeyName,
    string? WebhookUrl,
    IReadOnlyList<string>? WebhookEnabledEvents,
    Guid? ActorUserId,
    /// <summary>External product slug. Default <c>aura</c> for backward compatibility.</summary>
    string? ExternalProduct = null) : ICommand<ProvisionAuraWorkspaceResult>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
