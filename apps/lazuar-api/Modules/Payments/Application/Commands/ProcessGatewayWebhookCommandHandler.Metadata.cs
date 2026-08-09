using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Modules.Payments.Application.Services;

namespace Modules.Payments.Application.Commands;

public partial class ProcessGatewayWebhookCommandHandler
{
    /// <summary>
    /// Gap-fill merge: adapter metadata wins; session fills missing keys; force checkout_id from session.
    /// Lookup key is ProviderSessionId == GatewayTransactionId (Billplz bill id).
    /// </summary>
    private async Task<Dictionary<string, string>> MergeSessionMetadataAsync(
        Guid tenantId,
        string? gatewayTransactionId,
        Dictionary<string, string>? adapterMetadata,
        CancellationToken cancellationToken)
    {
        var metadata = adapterMetadata is { Count: > 0 }
            ? new Dictionary<string, string>(adapterMetadata, StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(gatewayTransactionId))
        {
            return metadata;
        }

        try
        {
            var session = await _sessions.GetByProviderSessionIdAsync(
                tenantId, gatewayTransactionId, cancellationToken);
            if (session is null)
            {
                return metadata;
            }

            var sessionMeta = IntegrationCheckoutMetadata.Deserialize(session.MetadataJson);
            foreach (var (key, value) in sessionMeta)
            {
                if (!metadata.ContainsKey(key))
                {
                    metadata[key] = value;
                }
            }

            // Always stamp correlation ids from the authoritative session row.
            metadata["checkout_id"] = session.Id.ToString();
            if (!metadata.ContainsKey("hub_workspace_id"))
            {
                metadata["hub_workspace_id"] = tenantId.ToString();
            }

            if (!metadata.ContainsKey("tenant_id"))
            {
                metadata["tenant_id"] = tenantId.ToString();
            }

            _logger.LogDebug(
                "Merged IntegrationCheckoutSession metadata for ProviderSessionId={ProviderSessionId} CheckoutId={CheckoutId} TenantId={TenantId}",
                gatewayTransactionId,
                session.Id,
                tenantId);
        }
        catch (Exception ex)
        {
            // Never fail money path on merge lookup errors — adapter metadata still publishes.
            _logger.LogWarning(
                ex,
                "Failed to merge IntegrationCheckoutSession metadata for GatewayTransactionId={GatewayTransactionId} TenantId={TenantId}",
                gatewayTransactionId,
                tenantId);
        }

        return metadata;
    }
}
