using System;
using System.Collections.Generic;

namespace Modules.One.Application.Commands;

public partial class ProvisionAuraWorkspaceCommandHandler
{
    private static ProvisionAuraWorkspaceResult BuildResult(
        Guid workspaceId,
        string slug,
        string product,
        string externalOrgId,
        bool created,
        Guid? apiKeyId,
        string? prefix,
        string? hint,
        string? plainKey,
        IReadOnlyList<string> scopes,
        Guid? webhookEndpointId,
        string? webhookUrl,
        bool? webhookIsActive,
        IReadOnlyList<string> webhookEnabledEvents,
        string? webhookSecretKey,
        string? webhookSecretHint,
        bool ownerAttached,
        string ownerStatus,
        string? ownerRole) =>
        new(
            workspaceId,
            slug,
            AuraOrgId: externalOrgId,
            Created: created,
            apiKeyId,
            prefix,
            hint,
            plainKey,
            scopes,
            webhookEndpointId,
            webhookUrl,
            webhookIsActive,
            webhookEnabledEvents,
            webhookSecretKey,
            webhookSecretHint,
            ownerAttached,
            ownerStatus,
            ownerRole,
            ExternalProduct: product,
            ExternalOrgId: externalOrgId);
}
