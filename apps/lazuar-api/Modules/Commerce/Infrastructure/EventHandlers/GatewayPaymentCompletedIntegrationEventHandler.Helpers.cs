using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Modules.Commerce.Application;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.Entities;
using Modules.Payments.Contracts;
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

    private async Task LogTransactionAsync(
        GatewayPaymentCompletedIntegrationEvent @event,
        Guid clientProfileId,
        string productName,
        string recordedBy,
        string? gatewayName,
        Guid? subscriptionId = null)
    {
        var clientProfile = await _crmQueryService.GetClientProfileAsync(clientProfileId);
        var customerName = clientProfile?.Full_name ?? "Unknown Customer";
        var customerEmail = clientProfile?.Email ?? string.Empty;

        var transactionLog = new CommerceTransactionLog(
            @event.OrganizationId,
            @event.AmountPaid,
            @event.GatewayFee,
            @event.Currency,
            CommerceTransactionLog.StatusConfirmed,
            customerName,
            customerEmail,
            productName,
            recordedBy,
            @event.GatewayTransactionId,
            gatewayName,
            subscriptionId);

        _dbContext.TransactionLogs.Add(transactionLog);
    }

    private static bool TryVaultIds(
        string gatewayName,
        string? customerId,
        string? tokenId,
        [NotNullWhen(true)] out string vaultCustomerId,
        [NotNullWhen(true)] out string vaultTokenId)
    {
        vaultCustomerId = "";
        vaultTokenId = "";

        if (PaymentGatewayCapabilities.IsReminderOnlyGateway(gatewayName))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(tokenId))
        {
            return false;
        }

        vaultTokenId = tokenId;
        vaultCustomerId = string.IsNullOrWhiteSpace(customerId) ? tokenId : customerId;
        return true;
    }

    private static void ApplyCheckoutMetadata(
        Subscription subscription,
        CheckoutSession session,
        GatewayPaymentCompletedIntegrationEvent @event,
        string? productInterval)
    {
        if (!string.IsNullOrWhiteSpace(session.MetadataJson))
        {
            subscription.SetMetadataJson(session.MetadataJson);
            return;
        }

        var persist = CommerceCheckoutMetadata.ForPersistence(@event.Metadata, productInterval);
        subscription.SetMetadataJson(CommerceCheckoutMetadata.Serialize(persist));
    }
}
