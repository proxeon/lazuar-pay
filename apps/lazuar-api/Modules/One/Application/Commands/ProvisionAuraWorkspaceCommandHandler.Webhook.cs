using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Modules.One.Domain;

namespace Modules.One.Application.Commands;

public partial class ProvisionAuraWorkspaceCommandHandler
{
    private readonly record struct WebhookEnsureState(
        Guid? Id,
        string? Url,
        bool? IsActive,
        IReadOnlyList<string> EnabledEvents,
        string? SecretKey,
        string? SecretHint);

    private (TenantWebhookEndpoint? Endpoint, string? Secret) TryCreateWebhookEndpoint(
        Guid organizationId,
        string? webhookUrl,
        IReadOnlyList<string> webhookEvents)
    {
        if (webhookUrl is null)
        {
            return (null, null);
        }

        var webhookSecret = MintWebhookSecret();
        var webhookEndpoint = new TenantWebhookEndpoint(
            organizationId,
            webhookUrl,
            webhookSecret,
            isActive: true,
            webhookEvents);
        _repository.AddWebhookEndpoint(webhookEndpoint);
        return (webhookEndpoint, webhookSecret);
    }

    private async Task<WebhookEnsureState> EnsureWebhookAsync(
        Guid organizationId,
        string? webhookUrl,
        IReadOnlyList<string> webhookEvents,
        CancellationToken ct)
    {
        // Webhook: exact URL match → metadata no secret; missing + URL given → create once.
        Guid? webhookId = null;
        string? webhookUrlOut = null;
        bool? webhookActive = null;
        IReadOnlyList<string> webhookEnabled = Array.Empty<string>();
        string? webhookSecret = null;
        string? webhookHint = null;
        var needsSave = false;

        var endpoints = await _repository.ListWebhookEndpointsAsync(organizationId, ct);

        if (webhookUrl is not null)
        {
            var match = endpoints.FirstOrDefault(e =>
                string.Equals(e.Url, webhookUrl, StringComparison.Ordinal));

            if (match is not null)
            {
                webhookId = match.Id;
                webhookUrlOut = match.Url;
                webhookActive = match.IsActive;
                webhookEnabled = match.EnabledEvents.ToList();
                webhookHint = string.IsNullOrEmpty(match.SecretKey) ? null : SecretHint(match.SecretKey);
                // secret once only — never remint
            }
            else
            {
                webhookSecret = MintWebhookSecret();
                var created = new TenantWebhookEndpoint(
                    organizationId,
                    webhookUrl,
                    webhookSecret,
                    isActive: true,
                    webhookEvents);
                _repository.AddWebhookEndpoint(created);
                needsSave = true;

                webhookId = created.Id;
                webhookUrlOut = created.Url;
                webhookActive = created.IsActive;
                webhookEnabled = created.EnabledEvents.ToList();
                webhookHint = SecretHint(webhookSecret);
            }
        }

        // Owner ensure saves immediately when membership is added. Webhook heal saves here.
        if (needsSave)
        {
            await _repository.SaveChangesAsync(ct);
        }

        return new WebhookEnsureState(
            webhookId,
            webhookUrlOut,
            webhookActive,
            webhookEnabled,
            webhookSecret,
            webhookHint);
    }

    private string MintWebhookSecret() =>
        "whsec_" + _tokenGenerator.GenerateSecureToken(24).PlainToken;

    private static string SecretHint(string secret) =>
        secret.Length >= 4 ? secret[^4..] : secret;
}
