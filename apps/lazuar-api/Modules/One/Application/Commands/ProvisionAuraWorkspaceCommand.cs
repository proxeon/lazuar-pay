using System;
using System.Collections.Generic;
using BuildingBlocks.Application;

namespace Modules.One.Application.Commands;

/// <remarks>
/// Integrator workspace provision. Type name is historical (Aura was the first client).
/// Canonical identity is (ExternalProduct, AuraOrgId-as-external-org-id).
/// </remarks>
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
    /// <summary>
    /// Required for new clients. HTTP resolver defaults to <c>aura</c> only when the body has
    /// <c>aura_org_id</c> and omits <c>external_org_id</c>.
    /// </summary>
    string? ExternalProduct = null) : ICommand<ProvisionAuraWorkspaceResult>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
