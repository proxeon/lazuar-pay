using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Contracts.Events;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.Entities;
using Modules.CRM.Contracts;

namespace Modules.Commerce.Application.Commands;

public class CreateManualSubscriberCommandHandler : ICommandHandler<CreateManualSubscriberCommand, Guid>
{
    private readonly ICommerceRepository _repository;
    private readonly IMediator _mediator;
    private readonly IEventBus _eventBus;
    private readonly ICrmQueryService _crmQueryService;

    public CreateManualSubscriberCommandHandler(
        ICommerceRepository repository,
        IMediator mediator,
        [FromKeyedServices("CommerceEventBus")] IEventBus eventBus,
        ICrmQueryService crmQueryService)
    {
        _repository = repository;
        _mediator = mediator;
        _eventBus = eventBus;
        _crmQueryService = crmQueryService;
    }

    public async Task<Guid> Handle(CreateManualSubscriberCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
        {
            throw new InvalidOperationException("A valid email address is required.");
        }

        var method = OfflinePaymentMethods.Normalize(request.PaymentMethod);
        var amount = method == OfflinePaymentMethods.Comped ? 0m : request.AmountPaid;
        if (amount < 0)
        {
            throw new InvalidOperationException("Payment amount cannot be negative.");
        }

        if (method != OfflinePaymentMethods.Comped && amount <= 0)
        {
            throw new InvalidOperationException("Amount paid must be greater than zero unless COMPED.");
        }

        var clientProfileId = await _mediator.Send(new ResolveClientProfileCommand(
            request.OrganizationId,
            request.Name,
            request.Email,
            request.Phone), ct);

        var product = await _repository.GetProductByIdAsync(request.ProductId, ct);
        if (product == null || product.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("Associated product catalog entry not found.");
        }

        if (!product.IsActive)
        {
            throw new InvalidOperationException("Cannot enroll against an archived product.");
        }

        if (product.Interval is not ("mo" or "yr"))
        {
            throw new InvalidOperationException("Manual enroll is only supported for recurring monthly or yearly products.");
        }

        var start = request.StartDate ?? DateTime.UtcNow;
        if (request.NextBillingDate.HasValue && request.NextBillingDate.Value < start)
        {
            throw new InvalidOperationException("Next billing date cannot be before the start date.");
        }

        if (await _repository.HasActiveSubscriptionAsync(request.OrganizationId, clientProfileId, product.Id, ct))
        {
            throw new InvalidOperationException("An active subscription already exists for this customer and product.");
        }

        var nextBillingDate = request.NextBillingDate
            ?? (product.Interval == "yr" ? start.AddYears(1) : start.AddMonths(1));

        var subscription = new Subscription(request.OrganizationId, clientProfileId, product.Id);
        subscription.Activate(start, nextBillingDate, isReminderOnly: true);
        _repository.AddSubscription(subscription);

        var clientProfile = await _crmQueryService.GetClientProfileAsync(clientProfileId);
        var clerkRef = string.IsNullOrWhiteSpace(request.ReferenceNumber) ? null : request.ReferenceNumber.Trim();
        var txLog = new CommerceTransactionLog(
            request.OrganizationId,
            amount,
            feeAmount: 0m,
            product.Currency,
            CommerceTransactionLog.StatusConfirmed,
            clientProfile?.Full_name ?? request.Name,
            clientProfile?.Email ?? request.Email,
            product.Name,
            recordedByName: method,
            externalReference: clerkRef,
            gatewayName: "OFFLINE",
            subscriptionId: subscription.Id);
        _repository.AddTransactionLog(txLog);

        if (amount > 0)
        {
            await _eventBus.PublishAsync(new ManualSubscriberEnrolledIntegrationEvent(
                request.OrganizationId,
                subscription.Id,
                clientProfileId,
                product.Id,
                amount,
                product.Currency,
                method,
                clerkRef,
                txLog.Id));
        }

        if (request.SendWelcomeEmail)
        {
            await _eventBus.PublishAsync(new SubscriptionActivatedIntegrationEvent(
                request.OrganizationId,
                subscription.Id,
                clientProfileId,
                product.Id,
                product.FulfillmentTargets.ToList(),
                IsFirstPayment: true));
        }

        await _repository.SaveChangesAsync(ct);
        return subscription.Id;
    }
}
