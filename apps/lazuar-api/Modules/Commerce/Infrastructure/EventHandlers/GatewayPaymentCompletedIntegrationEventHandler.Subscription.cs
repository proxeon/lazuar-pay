using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Modules.Commerce.Application;
using Modules.Commerce.Contracts.Events;
using Modules.Commerce.Domain.Entities;
using Modules.Payments.Contracts.Events;

namespace Modules.Commerce.Infrastructure.EventHandlers;

public partial class GatewayPaymentCompletedIntegrationEventHandler
{
    private async Task HandleSubscriptionPaymentAsync(
        GatewayPaymentCompletedIntegrationEvent @event,
        Guid subscriptionId)
    {
        var existingSub = await _dbContext.Subscriptions
            .IgnoreQueryFilters()
            .Include(s => s.ReminderLogs)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId && s.OrganizationId == @event.OrganizationId);

        if (existingSub == null)
        {
            return;
        }

        var productInfo = await _dbContext.Products
            .IgnoreQueryFilters()
            .Include(p => p.Prices)
            .FirstOrDefaultAsync(p => p.Id == existingSub.ProductId && p.OrganizationId == @event.OrganizationId);

        if (productInfo == null || productInfo.Interval == "one_time")
        {
            return;
        }

        var isMethodUpdateOnly = @event.Metadata != null
            && @event.Metadata.TryGetValue("update_payment", out var updateFlag)
            && updateFlag == "1"
            && existingSub.Status == "ACTIVE";

        if (isMethodUpdateOnly)
        {
            if (TryVaultIds(productInfo.GatewayName, @event.GatewayCustomerId, @event.GatewayTokenId, out var updateCustomerId, out var updateTokenId))
            {
                existingSub.StoreVaultedToken(updateCustomerId, updateTokenId);
            }

            await LogTransactionAsync(@event, existingSub.ClientProfileId, productInfo.Name, "SYSTEM", productInfo.GatewayName, existingSub.Id);
            await _repository.SaveChangesAsync();
            return;
        }

        // Already recovered / renewed this cycle (second hosted checkout or replay).
        // Off-session attempt 1 still has NextBillingDate in the past, so it continues below.
        if (existingSub.Status == "ACTIVE"
            && existingSub.NextBillingDate is { } paidThrough
            && paidThrough > DateTime.UtcNow)
        {
            if (TryVaultIds(productInfo.GatewayName, @event.GatewayCustomerId, @event.GatewayTokenId, out var dupCustomerId, out var dupTokenId))
            {
                existingSub.StoreVaultedToken(dupCustomerId, dupTokenId);
            }

            await LogTransactionAsync(@event, existingSub.ClientProfileId, productInfo.Name, "SYSTEM", productInfo.GatewayName, existingSub.Id);
            await _repository.SaveChangesAsync();
            return;
        }

        var wasInArrears = existingSub.Status is "PAST_DUE" or "SUSPENDED";
        var wasSuspended = existingSub.Status == "SUSPENDED";

        // Capture campaign id before ClearDunning (Resume / RecoverFromPayment).
        var recoveryCampaignId = DunningRecoveryAttribution.ResolveCampaignId(
            wasInArrears,
            existingSub.CurrentDunningCampaignId,
            @event.Metadata);

        var periodEnd = DateTime.UtcNow;
        var interval = SubscriptionBillingAmount.ResolveInterval(existingSub, productInfo);
        var updatedNextBilling = SubscriptionBillingAmount.AdvanceFrom(DateTime.UtcNow, interval);

        if (wasSuspended)
        {
            existingSub.Resume(updatedNextBilling);
        }
        else if (existingSub.Status == "PAST_DUE")
        {
            // Activate intentionally does not advance dates for PAST_DUE; recover explicitly.
            existingSub.RecoverFromPayment(periodEnd, updatedNextBilling);
        }
        else
        {
            existingSub.Activate(periodEnd, updatedNextBilling, existingSub.IsReminderOnly);
        }

        if (string.IsNullOrWhiteSpace(existingSub.MetadataJson))
        {
            var persist = CommerceCheckoutMetadata.ForPersistence(@event.Metadata, productInfo.Interval);
            existingSub.SetMetadataJson(CommerceCheckoutMetadata.Serialize(persist));
        }

        if (wasInArrears && recoveryCampaignId.HasValue)
        {
            var campaign = await _dbContext.DunningCampaigns
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == recoveryCampaignId.Value && c.OrganizationId == @event.OrganizationId);

            if (campaign != null)
            {
                campaign.RecordRecovery(@event.AmountPaid);
            }
        }

        if (wasSuspended)
        {
            await _eventBus.PublishAsync(new SubscriptionResumedIntegrationEvent(
                existingSub.OrganizationId,
                existingSub.Id,
                existingSub.ClientProfileId,
                existingSub.ProductId,
                productInfo.FulfillmentTargets.ToList()
            ));
        }
        else
        {
            await _eventBus.PublishAsync(new SubscriptionActivatedIntegrationEvent(
                existingSub.OrganizationId,
                existingSub.Id,
                existingSub.ClientProfileId,
                existingSub.ProductId,
                productInfo.FulfillmentTargets.ToList(),
                false
            ));
        }

        // Stripe/CHIP update-payment may convert reminder-only → vaulted. Billplz pay-again must not.
        if (TryVaultIds(productInfo.GatewayName, @event.GatewayCustomerId, @event.GatewayTokenId, out var vaultCustomerId, out var vaultTokenId))
        {
            existingSub.StoreVaultedToken(vaultCustomerId, vaultTokenId);
        }

        await MarkChargeAttemptSucceededAsync(@event, existingSub.Id);

        await LogTransactionAsync(@event, existingSub.ClientProfileId, productInfo.Name, "SYSTEM", productInfo.GatewayName, existingSub.Id);
        await _repository.SaveChangesAsync();
    }

    private async Task MarkChargeAttemptSucceededAsync(GatewayPaymentCompletedIntegrationEvent @event, Guid subscriptionId)
    {
        ChargeAttemptLog? attempt = null;

        if (@event.Metadata != null
            && @event.Metadata.TryGetValue("charge_attempt_id", out var attemptIdStr)
            && Guid.TryParse(attemptIdStr, out var attemptId))
        {
            attempt = await _dbContext.ChargeAttemptLogs
                .FirstOrDefaultAsync(l => l.Id == attemptId && l.SubscriptionId == subscriptionId);
        }

        attempt ??= await _dbContext.ChargeAttemptLogs
            .Where(l => l.SubscriptionId == subscriptionId && l.Status == ChargeAttemptLog.StatusPending)
            .OrderByDescending(l => l.AttemptNumber)
            .ThenByDescending(l => l.AttemptedAt)
            .FirstOrDefaultAsync();

        if (attempt == null)
        {
            return;
        }

        string? gatewayName = null;
        string? gatewayResponseCode = null;
        if (@event.Metadata != null)
        {
            @event.Metadata.TryGetValue("gateway_name", out gatewayName);
            @event.Metadata.TryGetValue("gateway_response_code", out gatewayResponseCode);
        }

        attempt.MarkSucceeded(gatewayName, gatewayResponseCode);
    }
}
