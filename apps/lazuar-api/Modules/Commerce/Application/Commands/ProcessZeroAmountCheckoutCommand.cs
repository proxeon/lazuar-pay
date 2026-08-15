using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Contracts.Events;

namespace Modules.Commerce.Application.Commands;

public record ProcessZeroAmountCheckoutCommand(Guid OrganizationId, Guid SessionId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class ProcessZeroAmountCheckoutCommandHandler : ICommandHandler<ProcessZeroAmountCheckoutCommand>
{
    private readonly ICommerceRepository _repository;
    private readonly IEventBus _eventBus;

    public ProcessZeroAmountCheckoutCommandHandler(
        ICommerceRepository repository,
        [FromKeyedServices("CommerceEventBus")] IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
    }

    public async Task Handle(ProcessZeroAmountCheckoutCommand request, CancellationToken ct)
    {
        var session = await _repository.GetCheckoutSessionByIdAsync(request.SessionId, ct);
        if (session == null || session.OrganizationId != request.OrganizationId || session.Status != "OPEN")
        {
            throw new InvalidOperationException("Checkout session is invalid or already processed.");
        }

        var product = await _repository.GetProductByIdAsync(session.ProductId ?? Guid.Empty, ct);
        if (product == null) throw new InvalidOperationException("Product not found.");

        var quantity = Math.Max(1, session.Quantity);
        var unitDiscount = 0m;
        var couponCode = "NONE";

        if (session.CouponId.HasValue)
        {
            var coupon = await _repository.GetCouponByIdAsync(session.CouponId.Value, ct);
            if (coupon != null)
            {
                unitDiscount = coupon.CalculateDiscount(product.Price);
                couponCode = coupon.Code;
                coupon.ConfirmReservation();
            }
        }

        var lineGross = product.Price * quantity;
        var lineDiscount = unitDiscount * quantity;
        var finalPrice = Math.Max(0, lineGross - lineDiscount);
        if (finalPrice > 0)
        {
            throw new InvalidOperationException("This checkout session requires payment and cannot bypass the gateway.");
        }

        session.Complete();

        if (product.Interval == "one_time")
        {
            var order = new Domain.Aggregates.Order(
                session.OrganizationId,
                session.ClientProfileId,
                product.Id,
                0m,
                product.Currency,
                quantity);
            order.Complete();
            _repository.AddOrder(order);

            await _eventBus.PublishAsync(new OrderCompletedIntegrationEvent(
                session.OrganizationId, order.Id, session.ClientProfileId, product.Id, product.FulfillmentTargets.ToList()));
        }
        else
        {
            var subscription = new Domain.Aggregates.Subscription(session.OrganizationId, session.ClientProfileId, product.Id);
            var nextBilling = product.Interval == "yr" ? DateTime.UtcNow.AddYears(1) : DateTime.UtcNow.AddMonths(1);
            subscription.Activate(DateTime.UtcNow, nextBilling, isReminderOnly: true);
            subscription.SetMetadataJson(session.MetadataJson);
            _repository.AddSubscription(subscription);

            await _eventBus.PublishAsync(new SubscriptionActivatedIntegrationEvent(
                session.OrganizationId, subscription.Id, session.ClientProfileId, product.Id, product.FulfillmentTargets.ToList(), true));
        }

        await _eventBus.PublishAsync(new ZeroAmountCheckoutCompletedIntegrationEvent(
            session.OrganizationId,
            session.Id,
            session.ClientProfileId,
            lineGross,
            lineDiscount,
            product.Currency,
            couponCode,
            new Dictionary<string, string>()));

        await _repository.SaveChangesAsync(ct);
    }
}
