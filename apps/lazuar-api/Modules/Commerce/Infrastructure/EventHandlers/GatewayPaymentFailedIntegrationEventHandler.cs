using System;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Commerce.Domain.Entities;
using Modules.Payments.Contracts.Events;

namespace Modules.Commerce.Infrastructure.EventHandlers;

/// <summary>
/// Bridges gateway payment failures into Commerce recovery: mark charge attempt failed, PAST_DUE + dunning campaign assignment.
/// </summary>
public class GatewayPaymentFailedIntegrationEventHandler : IIntegrationEventHandler<GatewayPaymentFailedIntegrationEvent>
{
    private readonly CommerceDbContext _dbContext;
    private readonly ILogger<GatewayPaymentFailedIntegrationEventHandler> _logger;

    public GatewayPaymentFailedIntegrationEventHandler(
        CommerceDbContext dbContext,
        ILogger<GatewayPaymentFailedIntegrationEventHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task HandleAsync(GatewayPaymentFailedIntegrationEvent @event)
    {
        if (!TryResolveSubscriptionId(@event, out var subscriptionId))
        {
            _logger.LogDebug(
                "GatewayPaymentFailed {GatewayTxId}: no subscription_id/receipt Guid in metadata; skipping commerce recovery.",
                @event.GatewayTransactionId);
            return;
        }

        var sub = await _dbContext.Subscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == subscriptionId && s.OrganizationId == @event.OrganizationId);

        if (sub == null)
        {
            _logger.LogDebug(
                "GatewayPaymentFailed {GatewayTxId}: subscription {SubscriptionId} not found for org {OrgId}.",
                @event.GatewayTransactionId, subscriptionId, @event.OrganizationId);
            return;
        }

        await MarkChargeAttemptFailedAsync(@event, sub.Id);

        if (sub.Status is "CANCELED" or "SUSPENDED")
        {
            _logger.LogInformation(
                "GatewayPaymentFailed {GatewayTxId}: subscription {SubscriptionId} is {Status}; charge attempt updated, skipping PAST_DUE.",
                @event.GatewayTransactionId, sub.Id, sub.Status);
            await _dbContext.SaveChangesAsync();
            return;
        }

        if (sub.Status != "PAST_DUE")
        {
            sub.MarkAsPastDue();
            _logger.LogInformation(
                "Subscription {SubscriptionId} marked PAST_DUE after payment failure {GatewayTxId}.",
                sub.Id, @event.GatewayTransactionId);
        }

        if (sub.CurrentDunningCampaignId == null)
        {
            // Same matching algorithm as DunningEngineJob: active campaigns by priority, product + payment method.
            var campaigns = await _dbContext.DunningCampaigns
                .IgnoreQueryFilters()
                .Where(c => c.IsActive)
                .OrderByDescending(c => c.PriorityOrder)
                .ThenByDescending(c => c.CreatedAt)
                .ToListAsync();

            var inferredPaymentMethod = string.IsNullOrEmpty(sub.VaultedTokenId) ? "MANUAL" : "ONLINE_GATEWAY";
            var campaignToAssign = campaigns.FirstOrDefault(c =>
                c.OrganizationId == sub.OrganizationId &&
                (c.TargetProductIds.Count == 0 || c.TargetProductIds.Contains(sub.ProductId)) &&
                (c.TargetPaymentMethods.Count == 0 || c.TargetPaymentMethods.Contains(inferredPaymentMethod)));

            if (campaignToAssign != null)
            {
                sub.AssignDunningCampaign(campaignToAssign.Id);
                _logger.LogInformation(
                    "Assigned dunning campaign {CampaignId} to subscription {SubscriptionId} after payment failure.",
                    campaignToAssign.Id, sub.Id);
            }
            else
            {
                _logger.LogWarning(
                    "No matching active dunning campaign for subscription {SubscriptionId} (org {OrgId}, product {ProductId}, method {Method}).",
                    sub.Id, sub.OrganizationId, sub.ProductId, inferredPaymentMethod);
            }
        }

        await _dbContext.SaveChangesAsync();
    }

    private async Task MarkChargeAttemptFailedAsync(GatewayPaymentFailedIntegrationEvent @event, Guid subscriptionId)
    {
        var attempt = await ResolveChargeAttemptAsync(@event, subscriptionId);
        if (attempt == null)
        {
            _logger.LogDebug(
                "GatewayPaymentFailed {GatewayTxId}: no PENDING ChargeAttemptLog for subscription {SubscriptionId}.",
                @event.GatewayTransactionId, subscriptionId);
            return;
        }

        string? failureReason = null;
        string? gatewayName = null;
        string? gatewayResponseCode = null;
        if (@event.Metadata != null)
        {
            @event.Metadata.TryGetValue("failure_reason", out failureReason);
            @event.Metadata.TryGetValue("gateway_name", out gatewayName);
            @event.Metadata.TryGetValue("gateway_response_code", out gatewayResponseCode);
        }

        attempt.MarkFailed(failureReason, gatewayName, gatewayResponseCode);
        _logger.LogInformation(
            "Marked ChargeAttemptLog {AttemptId} FAILED for subscription {SubscriptionId} (attempt {AttemptNumber}).",
            attempt.Id, subscriptionId, attempt.AttemptNumber);
    }

    private async Task<ChargeAttemptLog?> ResolveChargeAttemptAsync(
        GatewayPaymentFailedIntegrationEvent @event,
        Guid subscriptionId)
    {
        if (@event.Metadata != null
            && @event.Metadata.TryGetValue("charge_attempt_id", out var attemptIdStr)
            && Guid.TryParse(attemptIdStr, out var attemptId))
        {
            var byId = await _dbContext.ChargeAttemptLogs
                .FirstOrDefaultAsync(l => l.Id == attemptId && l.SubscriptionId == subscriptionId);
            if (byId != null)
            {
                return byId;
            }
        }

        return await _dbContext.ChargeAttemptLogs
            .Where(l => l.SubscriptionId == subscriptionId && l.Status == ChargeAttemptLog.StatusPending)
            .OrderByDescending(l => l.AttemptNumber)
            .ThenByDescending(l => l.AttemptedAt)
            .FirstOrDefaultAsync();
    }

    private static bool TryResolveSubscriptionId(GatewayPaymentFailedIntegrationEvent @event, out Guid subscriptionId)
    {
        subscriptionId = default;
        if (@event.Metadata == null)
        {
            return false;
        }

        if (@event.Metadata.TryGetValue("subscription_id", out var subIdStr)
            && Guid.TryParse(subIdStr, out subscriptionId))
        {
            return true;
        }

        if (@event.Metadata.TryGetValue("receipt", out var receipt)
            && Guid.TryParse(receipt, out subscriptionId))
        {
            return true;
        }

        return false;
    }
}
