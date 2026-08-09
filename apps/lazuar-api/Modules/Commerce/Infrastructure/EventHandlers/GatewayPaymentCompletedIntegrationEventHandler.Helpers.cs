using System;
using System.Threading.Tasks;
using Modules.Commerce.Domain.Entities;
using Modules.Payments.Contracts.Events;

namespace Modules.Commerce.Infrastructure.EventHandlers;

public partial class GatewayPaymentCompletedIntegrationEventHandler
{
    /// <summary>
    /// Prefer subscription_id; fall back to legacy receipt (off-session charges historically only set receipt).
    /// </summary>
    private static bool TryResolveCorrelationId(GatewayPaymentCompletedIntegrationEvent @event, out Guid correlationId)
    {
        correlationId = default;
        if (@event.Metadata == null)
        {
            return false;
        }

        if (@event.Metadata.TryGetValue("subscription_id", out var subIdStr)
            && Guid.TryParse(subIdStr, out correlationId))
        {
            return true;
        }

        if (@event.Metadata.TryGetValue("receipt", out var receipt)
            && Guid.TryParse(receipt, out correlationId))
        {
            return true;
        }

        return false;
    }

    private async Task LogTransactionAsync(GatewayPaymentCompletedIntegrationEvent @event, Guid clientProfileId, string productName, string recordedBy)
    {
        var clientProfile = await _crmQueryService.GetClientProfileAsync(clientProfileId);
        var customerName = clientProfile?.Full_name ?? "Unknown Customer";
        var customerEmail = clientProfile?.Email ?? string.Empty;

        var transactionLog = new CommerceTransactionLog(
            @event.OrganizationId,
            @event.AmountPaid,
            @event.GatewayFee,
            @event.Currency,
            "CONFIRMED",
            customerName,
            customerEmail,
            productName,
            recordedBy,
            @event.GatewayTransactionId
        );

        _dbContext.TransactionLogs.Add(transactionLog);
    }
}
