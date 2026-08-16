using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Contracts.Events;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.Entities;
using Modules.CRM.Contracts;

namespace Modules.Commerce.Application.Commands;

public class MarkCheckoutAsPaidOfflineCommandHandler : ICommandHandler<MarkCheckoutAsPaidOfflineCommand>
{
    private readonly ICommerceRepository _repository;
    private readonly IEventBus _eventBus;
    private readonly ICrmQueryService _crmQueryService;

    public MarkCheckoutAsPaidOfflineCommandHandler(
        ICommerceRepository repository,
        [FromKeyedServices("CommerceEventBus")] IEventBus eventBus,
        ICrmQueryService crmQueryService)
    {
        _repository = repository;
        _eventBus = eventBus;
        _crmQueryService = crmQueryService;
    }

    public async Task Handle(MarkCheckoutAsPaidOfflineCommand request, CancellationToken ct)
    {
        var session = await _repository.GetCheckoutSessionByIdAsync(request.SessionId, ct);

        if (session == null || session.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("Checkout session not found.");
        }

        if (session.Status != "OPEN")
        {
            throw new InvalidOperationException($"Cannot mark session as paid. Current status is {session.Status}.");
        }

        var clientProfile = await _crmQueryService.GetClientProfileAsync(session.ClientProfileId);
        var customerName = clientProfile?.Full_name ?? "Unknown Customer";
        var customerEmail = clientProfile?.Email ?? string.Empty;

        // Product checkout: create Sub/Order, confirm coupon, fulfillment events (parity with gateway paid / zero-amount).
        if (session.ProductId.HasValue)
        {
            await HandleProductSessionAsync(session, customerName, customerEmail, ct);
            return;
        }

        // Custom ad-hoc: COMPLETED + tx log + ledger; no fake product subscription.
        if (session.AdHocLineItems.Any())
        {
            await HandleCustomSessionAsync(session, customerName, customerEmail, ct);
            return;
        }

        throw new InvalidOperationException("Checkout session contains no billable items.");
    }

    private async Task HandleProductSessionAsync(
        CheckoutSession session,
        string customerName,
        string customerEmail,
        CancellationToken ct)
    {
        var product = await _repository.GetProductByIdAsync(session.ProductId!.Value, ct);
        if (product == null || product.OrganizationId != session.OrganizationId)
        {
            throw new InvalidOperationException("Associated product not found.");
        }

        var quantity = Math.Max(1, session.Quantity);
        var unitDiscount = 0m;
        if (session.CouponId.HasValue)
        {
            var coupon = await _repository.GetCouponByIdAsync(session.CouponId.Value, ct);
            if (coupon != null)
            {
                unitDiscount = coupon.CalculateDiscount(product.Price);
                coupon.ConfirmReservation();
            }
        }

        var lineGross = product.Price * quantity;
        var lineDiscount = unitDiscount * quantity;
        var totalAmount = Math.Max(0, lineGross - lineDiscount);
        var currency = product.Currency;

        session.Complete();

        Guid entitlementId;
        Guid? subscriptionId = null;

        if (product.Interval == "one_time")
        {
            var order = new Order(
                session.OrganizationId,
                session.ClientProfileId,
                product.Id,
                totalAmount,
                currency,
                quantity);
            order.Complete();
            _repository.AddOrder(order);
            entitlementId = order.Id;

            await _eventBus.PublishAsync(new OrderCompletedIntegrationEvent(
                session.OrganizationId,
                order.Id,
                session.ClientProfileId,
                product.Id,
                product.FulfillmentTargets.ToList()));
        }
        else
        {
            var subscription = new Subscription(session.OrganizationId, session.ClientProfileId, product.Id);
            var nextBilling = product.Interval == "yr" ? DateTime.UtcNow.AddYears(1) : DateTime.UtcNow.AddMonths(1);
            subscription.Activate(DateTime.UtcNow, nextBilling, isReminderOnly: true);
            subscription.SetMetadataJson(session.MetadataJson);
            _repository.AddSubscription(subscription);
            entitlementId = subscription.Id;
            subscriptionId = subscription.Id;

            await _eventBus.PublishAsync(new SubscriptionActivatedIntegrationEvent(
                session.OrganizationId,
                subscription.Id,
                session.ClientProfileId,
                product.Id,
                product.FulfillmentTargets.ToList(),
                IsFirstPayment: true));
        }

        var externalRef = $"OFFLINE-{session.Id:N}"[..36];
        var txLog = new CommerceTransactionLog(
            session.OrganizationId,
            totalAmount,
            feeAmount: 0m,
            currency,
            CommerceTransactionLog.StatusConfirmed,
            customerName,
            customerEmail,
            product.Name,
            recordedByName: "MANUAL_OFFLINE",
            externalReference: externalRef,
            gatewayName: "OFFLINE",
            subscriptionId: subscriptionId);
        _repository.AddTransactionLog(txLog);

        if (totalAmount > 0)
        {
            await _eventBus.PublishAsync(new ManualSubscriberEnrolledIntegrationEvent(
                session.OrganizationId,
                entitlementId,
                session.ClientProfileId,
                product.Id,
                totalAmount,
                currency,
                "MANUAL_OFFLINE",
                $"Manual settlement for session {session.Id}",
                txLog.Id));
        }

        await _repository.SaveChangesAsync(ct);
    }

    private async Task HandleCustomSessionAsync(
        CheckoutSession session,
        string customerName,
        string customerEmail,
        CancellationToken ct)
    {
        var totalAmount = session.AdHocLineItems.Sum(x => x.UnitPrice * x.Quantity);
        const string currency = "MYR";

        session.Complete();

        var externalRef = $"OFFLINE-{session.Id:N}"[..36];
        var txLog = new CommerceTransactionLog(
            session.OrganizationId,
            totalAmount,
            feeAmount: 0m,
            currency,
            CommerceTransactionLog.StatusConfirmed,
            customerName,
            customerEmail,
            productName: "Custom Payment Request",
            recordedByName: "MANUAL_OFFLINE",
            externalReference: externalRef,
            gatewayName: "OFFLINE");
        _repository.AddTransactionLog(txLog);

        if (totalAmount > 0)
        {
            // Ledger path only — event.SubscriptionId carries session id as stable CRM correlation.
            await _eventBus.PublishAsync(new ManualSubscriberEnrolledIntegrationEvent(
                session.OrganizationId,
                session.Id,
                session.ClientProfileId,
                ProductId: Guid.Empty,
                totalAmount,
                currency,
                "MANUAL_OFFLINE",
                $"Manual settlement for session {session.Id}",
                txLog.Id,
                session.IsB2bRequired));
        }

        await _repository.SaveChangesAsync(ct);
    }
}
