using System;
using System.Collections.Generic;

namespace Modules.One.Application.Commands;

public record ProvisionAuraWorkspaceResult(
    Guid WorkspaceId,
    string Slug,
    /// <summary>Normalized external org id (same as <see cref="ExternalOrgId"/>; kept for Aura clients).</summary>
    string AuraOrgId,
    bool Created,
    Guid? ApiKeyId,
    string? Prefix,
    string? Hint,
    string? PlainKey,
    IReadOnlyList<string> Scopes,
    // Webhook (null id when never registered / not requested and none exist)
    Guid? WebhookEndpointId,
    string? WebhookUrl,
    bool? WebhookIsActive,
    IReadOnlyList<string> WebhookEnabledEvents,
    string? WebhookSecretKey,
    string? WebhookSecretHint,
    // Owner
    bool OwnerAttached,
    string OwnerStatus,
    string? OwnerRole,
    /// <summary>Integrator product slug (e.g. aura, demo-app). Default aura.</summary>
    string ExternalProduct = "aura",
    /// <summary>External org / tenant id for that product (alias of AuraOrgId).</summary>
    string? ExternalOrgId = null);
