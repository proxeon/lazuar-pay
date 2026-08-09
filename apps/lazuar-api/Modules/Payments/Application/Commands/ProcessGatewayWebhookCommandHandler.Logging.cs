using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Modules.Payments.Application.Commands;

public partial class ProcessGatewayWebhookCommandHandler
{
    private void LogProcessed(
        ProcessGatewayWebhookCommand request,
        string eventId,
        string provider,
        string? gatewayTransactionId,
        string eventType,
        Dictionary<string, string>? metadata)
    {
        string? checkoutId = null;
        if (metadata is not null
            && metadata.TryGetValue("checkout_id", out var rawCheckoutId)
            && !string.IsNullOrWhiteSpace(rawCheckoutId))
        {
            checkoutId = rawCheckoutId;
        }

        _logger.LogInformation(
            "Payment webhook processed successfully. EventId={EventId} Provider={Provider} GatewayTransactionId={GatewayTransactionId} TenantId={TenantId} EventType={EventType} CheckoutId={CheckoutId}",
            eventId,
            provider,
            gatewayTransactionId ?? eventId,
            request.TenantId,
            eventType,
            checkoutId);
    }
}
