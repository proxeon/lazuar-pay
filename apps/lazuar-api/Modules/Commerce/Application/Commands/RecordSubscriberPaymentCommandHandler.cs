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
using Modules.One.Contracts;

namespace Modules.Commerce.Application.Commands;

public class RecordSubscriberPaymentCommandHandler : ICommandHandler<RecordSubscriberPaymentCommand>
{
    private readonly ICommerceRepository _repository;
    private readonly IEventBus _eventBus;
    private readonly ICrmQueryService _crmQueryService;
    private readonly IAuditRecorder? _auditRecorder;

    public RecordSubscriberPaymentCommandHandler(
        ICommerceRepository repository,
        [FromKeyedServices("CommerceEventBus")] IEventBus eventBus,
        ICrmQueryService crmQueryService,
        IAuditRecorder? auditRecorder = null)
    {
        _repository = repository;
        _eventBus = eventBus;
        _crmQueryService = crmQueryService;
        _auditRecorder = auditRecorder;
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

        var method = OfflinePaymentMethods.Normalize(request.PaymentMethod);
        var amount = method == OfflinePaymentMethods.Comped ? 0m : request.Amount;
        if (amount < 0)
        {
            throw new InvalidOperationException("Payment amount cannot be negative.");
        }

        var clerkRef = string.IsNullOrWhiteSpace(request.ReferenceNumber)
            ? null
            : request.ReferenceNumber.Trim();
        if (clerkRef != null)
        {
            var existing = await _repository.GetConfirmedTransactionLogByReferenceAsync(
                request.OrganizationId,
                subscription.Id,
                clerkRef,
                ct);
            if (existing != null)
            {
                return;
            }
        }

        var wasSuspended = subscription.Status == "SUSPENDED";
        var wasInArrears = subscription.Status is "PAST_DUE" or "SUSPENDED";

        var recoveryCampaignId = DunningRecoveryAttribution.ResolveCampaignId(
            wasInArrears,
            subscription.CurrentDunningCampaignId);

        var periodEnd = DateTime.UtcNow;
        var nextBilling = request.NextBillingDate
            ?? (product.Interval == "yr" ? periodEnd.AddYears(1) : periodEnd.AddMonths(1));

        if (wasInArrears)
        {
            subscription.RecoverFromPayment(periodEnd, nextBilling);
        }
        else
        {
            subscription.Activate(periodEnd, nextBilling, subscription.IsReminderOnly);
            subscription.ClearDunning();
        }

        if (recoveryCampaignId is Guid campaignId && amount > 0 && method != OfflinePaymentMethods.Comped)
        {
            var campaign = await _repository.GetDunningCampaignByIdAsync(request.OrganizationId, campaignId, ct);
            campaign?.RecordRecovery(amount);
        }

        var clientProfile = await _crmQueryService.GetClientProfileAsync(subscription.ClientProfileId);
        var customerName = clientProfile?.Full_name ?? "Unknown Customer";
        var customerEmail = clientProfile?.Email ?? string.Empty;

        var txLog = new CommerceTransactionLog(
            subscription.OrganizationId,
            amount,
            feeAmount: 0m,
            product.Currency,
            CommerceTransactionLog.StatusConfirmed,
            customerName,
            customerEmail,
            product.Name,
            recordedByName: method,
            externalReference: clerkRef,
            gatewayName: "OFFLINE",
            subscriptionId: subscription.Id);

        _repository.AddTransactionLog(txLog);

        if (amount > 0 && method != OfflinePaymentMethods.Comped)
        {
            await _eventBus.PublishAsync(new ManualSubscriberEnrolledIntegrationEvent(
                subscription.OrganizationId,
                subscription.Id,
                subscription.ClientProfileId,
                subscription.ProductId,
                amount,
                product.Currency,
                method,
                clerkRef,
                txLog.Id));
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
        else if (wasInArrears)
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

        if (_auditRecorder != null)
        {
            await _auditRecorder.RecordAsync(
                request.OrganizationId,
                "subscriber.payment_recorded",
                "subscription",
                request.SubscriptionId.ToString(),
                new { amount, method, transaction_id = txLog.Id.ToString() },
                ct: ct);
        }
    }
}
