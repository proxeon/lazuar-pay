using System;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Application;
using Modules.Commerce.Contracts.Events;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.Entities;
using Modules.CRM.Contracts;
using Modules.Payments.Contracts.Events;

namespace Modules.Commerce.Infrastructure.EventHandlers;

public class GatewayPaymentCompletedIntegrationEventHandler : IIntegrationEventHandler<GatewayPaymentCompletedIntegrationEvent>
{
    private readonly ICommerceRepository _repository;
    private readonly IEventBus _eventBus;
    private readonly ICrmQueryService _crmQueryService;
    private readonly CommerceDbContext _dbContext;

    public GatewayPaymentCompletedIntegrationEventHandler(
        ICommerceRepository repository,
        [FromKeyedServices("CommerceEventBus")] IEventBus eventBus,
        ICrmQueryService crmQueryService,
        CommerceDbContext dbContext)
    {
        _repository = repository;
        _eventBus = eventBus;
        _crmQueryService = crmQueryService;
        _dbContext = dbContext;
    }

    public async Task HandleAsync(GatewayPaymentCompletedIntegrationEvent @event)
    {
        var type = @event.Metadata.GetValueOrDefault("type");
        if (type != "commerce_subscription" && type != "custom_payment_link")
        {
            return;
        }

        if (!TryResolveCorrelationId(@event, out var correlationId))
        {
            return;
        }

        // Session path: open checkout session (initial subscribe / custom payment link).
        var session = await _repository.GetCheckoutSessionByIdAsync(correlationId);
        if (session != null && session.Status == "OPEN")
        {
            if (session.OrganizationId != @event.OrganizationId)
            {
                return;
            }

            await HandleOpenCheckoutSessionAsync(@event, session, type!);
            return;
        }

        // Subscription recovery / renewal path (off-session charge, update-payment, etc.).
        await HandleSubscriptionPaymentAsync(@event, correlationId);
    }

    private async Task HandleOpenCheckoutSessionAsync(
        GatewayPaymentCompletedIntegrationEvent @event,
        CheckoutSession session,
        string type)
    {
        session.Complete();

        if (type == "custom_payment_link")
        {
            await LogTransactionAsync(@event, session.ClientProfileId, "Custom Payment Request", "SYSTEM");
            await _repository.SaveChangesAsync();
            return;
        }

        var product = await _repository.GetProductByIdAsync(session.ProductId ?? Guid.Empty);
        if (product == null)
        {
            throw new InvalidOperationException($"Product associated with session {session.Id} not found.");
        }

        if (product.Interval != "one_time")
        {
            var subscription = new Subscription(
                session.OrganizationId,
                session.ClientProfileId,
                product.Id
            );

            var nextBilling = product.Interval == "yr" ? DateTime.UtcNow.AddYears(1) : DateTime.UtcNow.AddMonths(1);
            subscription.Activate(DateTime.UtcNow, nextBilling);

            if (!string.IsNullOrEmpty(@event.GatewayCustomerId) && !string.IsNullOrEmpty(@event.GatewayTokenId))
            {
                subscription.StoreVaultedToken(@event.GatewayCustomerId, @event.GatewayTokenId);
            }

            _repository.AddSubscription(subscription);

            await _eventBus.PublishAsync(new SubscriptionActivatedIntegrationEvent(
                subscription.OrganizationId,
                subscription.Id,
                subscription.ClientProfileId,
                subscription.ProductId,
                product.FulfillmentTargets.ToList(),
                true
            ));
        }
        else
        {
            var order = new Order(
                session.OrganizationId,
                session.ClientProfileId,
                product.Id,
                @event.AmountPaid,
                product.Currency
            );

            order.Complete();
            _repository.AddOrder(order);

            await _eventBus.PublishAsync(new OrderCompletedIntegrationEvent(
                order.OrganizationId,
                order.Id,
                order.ClientProfileId,
                order.ProductId,
                product.FulfillmentTargets.ToList()
            ));
        }

        await LogTransactionAsync(@event, session.ClientProfileId, product.Name, "SYSTEM");
        await _repository.SaveChangesAsync();
    }

    private async Task HandleSubscriptionPaymentAsync(
        GatewayPaymentCompletedIntegrationEvent @event,
        Guid subscriptionId)
    {
        var existingSub = await _repository.GetSubscriptionByIdAsync(subscriptionId);
        if (existingSub == null || existingSub.OrganizationId != @event.OrganizationId)
        {
            return;
        }

        var productInfo = await _repository.GetProductByIdAsync(existingSub.ProductId);
        if (productInfo == null || productInfo.Interval == "one_time")
        {
            return;
        }

        var wasInArrears = existingSub.Status is "PAST_DUE" or "SUSPENDED";
        var wasSuspended = existingSub.Status == "SUSPENDED";

        // Capture campaign id before ClearDunning (Resume / RecoverFromPayment).
        Guid? recoveryCampaignId = null;
        if (wasInArrears)
        {
            if (@event.Metadata.TryGetValue("dunning_campaign_id", out var dunningCampaignIdStr)
                && Guid.TryParse(dunningCampaignIdStr, out var fromMetadata))
            {
                recoveryCampaignId = fromMetadata;
            }
            else
            {
                recoveryCampaignId = existingSub.CurrentDunningCampaignId;
            }
        }

        var periodEnd = DateTime.UtcNow;
        var updatedNextBilling = productInfo.Interval == "yr"
            ? DateTime.UtcNow.AddYears(1)
            : DateTime.UtcNow.AddMonths(1);

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

        // Store/refresh vault tokens when present (e.g. update-payment flow).
        if (!string.IsNullOrEmpty(@event.GatewayCustomerId) && !string.IsNullOrEmpty(@event.GatewayTokenId))
        {
            existingSub.StoreVaultedToken(@event.GatewayCustomerId, @event.GatewayTokenId);
        }

        await LogTransactionAsync(@event, existingSub.ClientProfileId, productInfo.Name, "SYSTEM");
        await _repository.SaveChangesAsync();
    }

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
