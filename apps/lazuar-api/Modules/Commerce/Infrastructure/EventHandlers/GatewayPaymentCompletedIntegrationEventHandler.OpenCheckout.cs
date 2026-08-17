using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Modules.Commerce.Contracts.Events;
using Modules.Commerce.Domain.Aggregates;
using Modules.Payments.Contracts.Events;

namespace Modules.Commerce.Infrastructure.EventHandlers;

public partial class GatewayPaymentCompletedIntegrationEventHandler
{
    private async Task HandleOpenCheckoutSessionAsync(
        GatewayPaymentCompletedIntegrationEvent @event,
        CheckoutSession session,
        string type)
    {
        // Confirm coupon reservation when payment completes for a product checkout.
        if (session.CouponId.HasValue)
        {
            var coupon = await _dbContext.Coupons
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == session.CouponId.Value && c.OrganizationId == session.OrganizationId);

            if (coupon != null && coupon.ReservedCount > 0)
            {
                coupon.ConfirmReservation();
            }
        }

        if (!session.TryComplete())
        {
            return;
        }

        if (type == "custom_payment_link")
        {
            await LogTransactionAsync(@event, session.ClientProfileId, "Custom Payment Request", "SYSTEM", session.GatewayName);

            var payloadObj = new
            {
                amount = @event.AmountPaid,
                currency = @event.Currency,
                gateway_transaction_id = @event.GatewayTransactionId,
                status = "PAID",
                checkout_session_id = session.Id.ToString(),
                client_profile_id = session.ClientProfileId.ToString()
            };
            var payloadElement = System.Text.Json.JsonSerializer.SerializeToElement(
                payloadObj,
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower });

            await _eventBus.PublishAsync(new OutboundWebhookRequestedIntegrationEvent(
                session.OrganizationId,
                TargetUrl: null,
                "payment_link.paid",
                payloadElement));

            await TrySaveSessionCompletionAsync();
            return;
        }

        var product = await _dbContext.Products
            .IgnoreQueryFilters()
            .Include(p => p.Prices)
            .FirstOrDefaultAsync(p =>
                p.Id == (session.ProductId ?? Guid.Empty)
                && p.OrganizationId == session.OrganizationId);

        if (product == null)
        {
            throw new InvalidOperationException($"Product associated with session {session.Id} not found.");
        }

        // Resource ownership: product must belong to the same org as the checkout session / event.
        if (product.OrganizationId != @event.OrganizationId)
        {
            return;
        }

        Guid? subscriptionId = null;
        if (product.Interval != "one_time")
        {
            var subscription = new Subscription(
                session.OrganizationId,
                session.ClientProfileId,
                product.Id
            );

            var chosen = product.Prices.FirstOrDefault(p => p.Id == session.PriceId);
            var unitAmount = chosen?.Amount ?? product.Price;
            var interval = chosen?.Interval ?? product.Interval;
            var hasVault = TryVaultIds(product.GatewayName, @event.GatewayCustomerId, @event.GatewayTokenId, out var vaultCustomerId, out var vaultTokenId);
            Modules.Commerce.Application.SubscriptionActivation.Start(
                subscription,
                product,
                Math.Max(1, session.Quantity),
                unitAmount,
                reminderOnly: !hasVault,
                billingInterval: interval,
                priceId: session.PriceId);
            ApplyCheckoutMetadata(subscription, session, @event, interval);

            if (hasVault)
            {
                subscription.StoreVaultedToken(vaultCustomerId, vaultTokenId);
            }

            _repository.AddSubscription(subscription);
            subscriptionId = subscription.Id;

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
                product.Currency,
                session.Quantity
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

        var gatewayName = !string.IsNullOrWhiteSpace(product.GatewayName) ? product.GatewayName : session.GatewayName;
        await LogTransactionAsync(@event, session.ClientProfileId, product.Name, "SYSTEM", gatewayName, subscriptionId);
        await TrySaveSessionCompletionAsync();
    }

    private async Task TrySaveSessionCompletionAsync()
    {
        try
        {
            await _repository.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another completer already persisted COMPLETED for this OPEN row.
        }
    }
}
