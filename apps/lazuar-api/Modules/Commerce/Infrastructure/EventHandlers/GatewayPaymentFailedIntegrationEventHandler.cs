using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modules.Commerce.Application;
using Modules.Commerce.Contracts.Events;
using Modules.Commerce.Domain.Entities;
using Modules.Commerce.Infrastructure.Dunning;
using Modules.CRM.Contracts;
using Modules.Payments.Contracts.Events;

namespace Modules.Commerce.Infrastructure.EventHandlers;

/// <summary>
/// Bridges gateway payment failures into Commerce recovery: fail the attempt, PAST_DUE + assign, and start the dunning run.
/// Emits <c>subscription.past_due</c> when status first changes to PAST_DUE.
/// </summary>
public class GatewayPaymentFailedIntegrationEventHandler : IIntegrationEventHandler<GatewayPaymentFailedIntegrationEvent>
{
    private readonly CommerceDbContext _dbContext;
    private readonly IEventBus _eventBus;
    private readonly ICrmQueryService _crmQueryService;
    private readonly ILogger<GatewayPaymentFailedIntegrationEventHandler> _logger;
    private readonly IConfiguration _configuration;

    public GatewayPaymentFailedIntegrationEventHandler(
        CommerceDbContext dbContext,
        [FromKeyedServices("CommerceEventBus")] IEventBus eventBus,
        ICrmQueryService crmQueryService,
        ILogger<GatewayPaymentFailedIntegrationEventHandler> logger,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _eventBus = eventBus;
        _crmQueryService = crmQueryService;
        _logger = logger;
        _configuration = configuration;
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
            .Include(s => s.ReminderLogs)
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

        var becamePastDue = sub.Status != "PAST_DUE";
        if (becamePastDue)
        {
            sub.MarkAsPastDue();
            _logger.LogInformation(
                "Subscription {SubscriptionId} marked PAST_DUE after payment failure {GatewayTxId}.",
                sub.Id, @event.GatewayTransactionId);
        }

        var campaigns = await PastDueDunningProcessor.LoadActiveCampaignsAsync(_dbContext, CancellationToken.None);
        var whatsAppEnabled = _configuration.GetValue("Messaging:WhatsAppEnabled", false);
        var processor = new PastDueDunningProcessor(_logger);
        await processor.ProcessAsync(_dbContext, _eventBus, sub, campaigns, whatsAppEnabled, CancellationToken.None);

        if (becamePastDue)
        {
            await PublishPastDueAsync(sub);
        }

        await _dbContext.SaveChangesAsync();
    }

    private async Task PublishPastDueAsync(Domain.Aggregates.Subscription sub)
    {
        var product = await _dbContext.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == sub.ProductId);
        var profile = await _crmQueryService.GetClientProfileAsync(sub.ClientProfileId);
        var payload = CommerceWebhookPayload.From(sub, product, profile?.Email, "PAST_DUE");

        await _eventBus.PublishAsync(new OutboundWebhookRequestedIntegrationEvent(
            sub.OrganizationId, TargetUrl: null, "subscription.past_due", payload));
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
