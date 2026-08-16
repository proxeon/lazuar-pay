using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Commerce.Domain.Entities;
using Modules.Payments.Contracts;
using Modules.Payments.Contracts.Events;

namespace Modules.Commerce.Infrastructure.EventHandlers;

/// <summary>
/// Persists Commerce GMV disputes. Platform utility / Hub SaaS types are owned by Billing.
/// Does not cancel the subscription or book the dispute as a refund.
/// </summary>
public class CommerceGatewayDisputeCreatedHandler : IIntegrationEventHandler<GatewayDisputeCreatedIntegrationEvent>
{
    private readonly CommerceDbContext _dbContext;
    private readonly ILogger<CommerceGatewayDisputeCreatedHandler> _logger;

    public CommerceGatewayDisputeCreatedHandler(
        CommerceDbContext dbContext,
        ILogger<CommerceGatewayDisputeCreatedHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task HandleAsync(GatewayDisputeCreatedIntegrationEvent @event)
    {
        if (@event.Metadata != null
            && @event.Metadata.TryGetValue("type", out var type)
            && PlatformCheckoutTypes.IsPlatformCollected(type))
        {
            return;
        }

        var existing = await _dbContext.Disputes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d =>
                d.OrganizationId == @event.OrganizationId
                && d.GatewayTransactionId == @event.GatewayTransactionId);

        if (existing != null)
        {
            await TryMarkHasOpenDisputeAsync(@event);
            return;
        }

        TryResolveLinks(@event, out var subscriptionId, out var checkoutSessionId);

        if (subscriptionId.HasValue)
        {
            var sub = await _dbContext.Subscriptions
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.Id == subscriptionId.Value && s.OrganizationId == @event.OrganizationId);
            if (sub == null)
            {
                if (checkoutSessionId == null)
                {
                    var session = await _dbContext.CheckoutSessions
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(s =>
                            s.Id == subscriptionId.Value && s.OrganizationId == @event.OrganizationId);
                    if (session != null)
                    {
                        checkoutSessionId = session.Id;
                        subscriptionId = null;
                    }
                    else
                    {
                        subscriptionId = null;
                    }
                }
                else
                {
                    subscriptionId = null;
                }
            }
            else
            {
                sub.MarkHasOpenDispute();
            }
        }

        var dispute = new CommerceDispute(
            @event.OrganizationId,
            @event.GatewayTransactionId,
            @event.AmountDisputed,
            @event.Currency,
            subscriptionId,
            checkoutSessionId);
        _dbContext.Disputes.Add(dispute);

        var log = await _dbContext.TransactionLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(l =>
                l.OrganizationId == @event.OrganizationId
                && l.ExternalReference == @event.GatewayTransactionId);
        log?.MarkDisputed();

        await _dbContext.SaveChangesAsync();
        _logger.LogWarning(
            "Recorded OPEN commerce dispute {DisputeId} for gateway tx {GatewayTxId} org {OrgId}.",
            dispute.Id, @event.GatewayTransactionId, @event.OrganizationId);
    }

    private async Task TryMarkHasOpenDisputeAsync(GatewayDisputeCreatedIntegrationEvent @event)
    {
        if (@event.Metadata == null
            || !TryGuid(@event.Metadata, "subscription_id", out var subscriptionId))
        {
            return;
        }

        var sub = await _dbContext.Subscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == subscriptionId && s.OrganizationId == @event.OrganizationId);
        if (sub == null)
        {
            return;
        }

        sub.MarkHasOpenDispute();
        await _dbContext.SaveChangesAsync();
    }

    private static void TryResolveLinks(
        GatewayDisputeCreatedIntegrationEvent @event,
        out Guid? subscriptionId,
        out Guid? checkoutSessionId)
    {
        subscriptionId = null;
        checkoutSessionId = null;
        if (@event.Metadata == null)
        {
            return;
        }

        if (TryGuid(@event.Metadata, "subscription_id", out var subId))
        {
            subscriptionId = subId;
        }

        if (TryGuid(@event.Metadata, "checkout_session_id", out var sessionId)
            || TryGuid(@event.Metadata, "session_id", out sessionId)
            || TryGuid(@event.Metadata, "checkout_id", out sessionId))
        {
            checkoutSessionId = sessionId;
        }
    }

    private static bool TryGuid(
        System.Collections.Generic.IReadOnlyDictionary<string, string> metadata,
        string key,
        out Guid value)
    {
        value = default;
        return metadata.TryGetValue(key, out var raw) && Guid.TryParse(raw, out value);
    }
}
