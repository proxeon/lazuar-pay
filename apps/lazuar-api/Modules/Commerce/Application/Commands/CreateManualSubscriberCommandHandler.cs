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
using Modules.CRM.Contracts;

namespace Modules.Commerce.Application.Commands;

public class CreateManualSubscriberCommandHandler : ICommandHandler<CreateManualSubscriberCommand, Guid>
{
    private readonly ICommerceRepository _repository;
    private readonly IMediator _mediator;
    private readonly IEventBus _eventBus;

    public CreateManualSubscriberCommandHandler(
        ICommerceRepository repository,
        IMediator mediator,
        [FromKeyedServices("CommerceEventBus")] IEventBus eventBus)
    {
        _repository = repository;
        _mediator = mediator;
        _eventBus = eventBus;
    }

    public async Task<Guid> Handle(CreateManualSubscriberCommand request, CancellationToken ct)
    {
        var resolveCrmProfileCmd = new ResolveClientProfileCommand(
            request.OrganizationId,
            request.Name,
            request.Email,
            request.Phone
        );
        var clientProfileId = await _mediator.Send(resolveCrmProfileCmd, ct);

        var product = await _repository.GetProductByIdAsync(request.ProductId, ct);
        if (product == null || product.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("Associated product catalog entry not found.");
        }

        DateTime currentPeriodEnd = request.StartDate ?? DateTime.UtcNow;
        DateTime? nextBillingDate = null;

        if (product.Interval != "one_time")
        {
            nextBillingDate = request.NextBillingDate ?? (product.Interval == "yr" ? currentPeriodEnd.AddYears(1) : currentPeriodEnd.AddMonths(1));
        }

        var subscription = new Subscription(
            request.OrganizationId,
            clientProfileId,
            product.Id
        );

        subscription.Activate(currentPeriodEnd, nextBillingDate, isReminderOnly: true);
        _repository.AddSubscription(subscription);

        if (request.AmountPaid > 0 && request.PaymentMethod != "COMPED")
        {
            var ledgerEvent = new ManualSubscriberEnrolledIntegrationEvent(
                request.OrganizationId,
                subscription.Id,
                clientProfileId,
                product.Id,
                request.AmountPaid,
                product.Currency,
                request.PaymentMethod,
                request.ReferenceNumber
            );
            await _eventBus.PublishAsync(ledgerEvent);
        }

        if (request.SendWelcomeEmail)
        {
            var activatedEvent = new SubscriptionActivatedIntegrationEvent(
                request.OrganizationId,
                subscription.Id,
                clientProfileId,
                product.Id,
                product.FulfillmentTargets.ToList(),
                IsFirstPayment: true
            );
            await _eventBus.PublishAsync(activatedEvent);
        }

        await _repository.SaveChangesAsync(ct);
        return subscription.Id;
    }
}
