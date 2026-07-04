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

        if ((type == "commerce_subscription" || type == "custom_payment_link") && @event.Metadata.TryGetValue("subscription_id", out var sessionIdStr) && Guid.TryParse(sessionIdStr, out var sessionId))
        {
            var session = await _repository.GetCheckoutSessionByIdAsync(sessionId);
            if (session == null || session.Status == "COMPLETED")
            {
                var existingSub = await _repository.GetSubscriptionByIdAsync(sessionId);
                if (existingSub != null)
                {
                    var productInfo = await _repository.GetProductByIdAsync(existingSub.ProductId);
                    if (productInfo != null && productInfo.Interval != "one_time")
                    {
                        var wasInArrears = existingSub.Status == "PAST_DUE" || existingSub.Status == "SUSPENDED";
                        var wasSuspended = existingSub.Status == "SUSPENDED";

                        var updatedNextBilling = productInfo.Interval == "yr" ? DateTime.UtcNow.AddYears(1) : DateTime.UtcNow.AddMonths(1);
                        
                        if (wasSuspended)
                        {
                            existingSub.Resume(updatedNextBilling);
                        }
                        else
                        {
                            existingSub.Activate(DateTime.UtcNow, updatedNextBilling, existingSub.IsReminderOnly);
                            existingSub.ClearDunning();
                        }

                        if (wasInArrears && @event.Metadata.TryGetValue("dunning_campaign_id", out var dunningCampaignIdStr) && Guid.TryParse(dunningCampaignIdStr, out var dunningCampaignId))
                        {
                            var campaign = await _dbContext.DunningCampaigns
                                .IgnoreQueryFilters()
                                .FirstOrDefaultAsync(c => c.Id == dunningCampaignId && c.OrganizationId == @event.OrganizationId);

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

                        await LogTransactionAsync(@event, existingSub.ClientProfileId, productInfo.Name, "SYSTEM");
                        await _repository.SaveChangesAsync();
                    }
                }
                return;
            }

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
                throw new InvalidOperationException($"Product associated with session {sessionId} not found.");
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
