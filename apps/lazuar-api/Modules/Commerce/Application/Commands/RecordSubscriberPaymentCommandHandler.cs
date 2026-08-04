using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Contracts.Events;
using Modules.Commerce.Domain.Entities;
using Modules.CRM.Contracts;

namespace Modules.Commerce.Application.Commands;

public class RecordSubscriberPaymentCommandHandler : ICommandHandler<RecordSubscriberPaymentCommand>
{
    private readonly ICommerceRepository _repository;
    private readonly IEventBus _eventBus;
    private readonly ICrmQueryService _crmQueryService;

    public RecordSubscriberPaymentCommandHandler(
        ICommerceRepository repository,
        [FromKeyedServices("CommerceEventBus")] IEventBus eventBus,
        ICrmQueryService crmQueryService)
    {
        _repository = repository;
        _eventBus = eventBus;
        _crmQueryService = crmQueryService;
    }

    public async Task Handle(RecordSubscriberPaymentCommand request, CancellationToken ct)
    {
        var subscription = await _repository.GetSubscriptionByIdAsync(request.SubscriptionId, ct);
        if (subscription == null || subscription.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("Subscription not found.");
        }

        if (subscription.Status is "CANCELED" or "PENDING")
        {
            throw new InvalidOperationException($"Cannot record payment for subscription in status '{subscription.Status}'.");
        }

        var product = await _repository.GetProductByIdAsync(subscription.ProductId, ct);
        if (product == null || product.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("Associated product not found.");
        }

        if (product.Interval == "one_time")
        {
            throw new InvalidOperationException("Record-payment is only supported for recurring subscriptions.");
        }

        var amount = request.Amount;
        var method = (request.PaymentMethod ?? "MANUAL").Trim().ToUpperInvariant();
        if (method == "COMPED")
        {
            amount = 0m;
        }

        if (amount < 0)
        {
            throw new InvalidOperationException("Payment amount cannot be negative.");
        }

        var wasSuspended = subscription.Status == "SUSPENDED";
        var wasInArrears = subscription.Status is "PAST_DUE" or "SUSPENDED";

        var periodEnd = DateTime.UtcNow;
        var nextBilling = product.Interval == "yr"
            ? DateTime.UtcNow.AddYears(1)
            : DateTime.UtcNow.AddMonths(1);

        if (wasSuspended)
        {
            subscription.Resume(nextBilling);
        }
        else if (subscription.Status == "PAST_DUE")
        {
            subscription.RecoverFromPayment(periodEnd, nextBilling);
        }
        else
        {
            // ACTIVE renewal: advance period and clear any residual dunning pause.
            subscription.Activate(periodEnd, nextBilling, subscription.IsReminderOnly);
            subscription.ClearDunning();
        }

        var clientProfile = await _crmQueryService.GetClientProfileAsync(subscription.ClientProfileId);
        var customerName = clientProfile?.Full_name ?? "Unknown Customer";
        var customerEmail = clientProfile?.Email ?? string.Empty;

        var externalRef = string.IsNullOrWhiteSpace(request.ReferenceNumber)
            ? $"MANUAL-{subscription.Id:N}"[..32]
            : request.ReferenceNumber.Trim();

        var txLog = new CommerceTransactionLog(
            subscription.OrganizationId,
            amount,
            feeAmount: 0m,
            product.Currency,
            "CONFIRMED",
            customerName,
            customerEmail,
            product.Name,
            recordedByName: method,
            externalReference: externalRef);

        _repository.AddTransactionLog(txLog);

        if (amount > 0 && method != "COMPED")
        {
            await _eventBus.PublishAsync(new ManualSubscriberEnrolledIntegrationEvent(
                subscription.OrganizationId,
                subscription.Id,
                subscription.ClientProfileId,
                subscription.ProductId,
                amount,
                product.Currency,
                method,
                request.ReferenceNumber));
        }

        if (wasSuspended)
        {
            await _eventBus.PublishAsync(new SubscriptionResumedIntegrationEvent(
                subscription.OrganizationId,
                subscription.Id,
                subscription.ClientProfileId,
                subscription.ProductId,
                product.FulfillmentTargets.ToList()));
        }
        else if (wasInArrears || amount > 0)
        {
            await _eventBus.PublishAsync(new SubscriptionActivatedIntegrationEvent(
                subscription.OrganizationId,
                subscription.Id,
                subscription.ClientProfileId,
                subscription.ProductId,
                product.FulfillmentTargets.ToList(),
                IsFirstPayment: false));
        }

        await _repository.SaveChangesAsync(ct);
    }
}
