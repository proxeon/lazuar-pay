using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Contracts.Events;
using Modules.Commerce.Domain.Aggregates;

namespace Modules.Commerce.Application.Commands;

public class MarkCheckoutAsPaidOfflineCommandHandler : ICommandHandler<MarkCheckoutAsPaidOfflineCommand>
{
    private readonly ICommerceRepository _repository;
    private readonly IEventBus _eventBus;

    public MarkCheckoutAsPaidOfflineCommandHandler(
        ICommerceRepository repository,
        [FromKeyedServices("CommerceEventBus")] IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
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

        decimal totalAmount = 0m;
        string currency = "MYR";

        if (session.ProductId.HasValue)
        {
            var product = await _repository.GetProductByIdAsync(session.ProductId.Value, ct);
            if (product == null) throw new InvalidOperationException("Associated product not found.");
            
            totalAmount = product.Price;
            currency = product.Currency;
        }
        else if (session.AdHocLineItems.Any())
        {
            totalAmount = session.AdHocLineItems.Sum(x => x.UnitPrice * x.Quantity);
        }
        else
        {
            throw new InvalidOperationException("Checkout session contains no billable items.");
        }

        session.Complete();

        // Simulate a manual gateway payload so the downstream Billing module records it instantly.
        var integrationEvent = new ManualSubscriberEnrolledIntegrationEvent(
            session.OrganizationId,
            session.Id,
            session.ClientProfileId,
            session.ProductId ?? Guid.Empty,
            totalAmount,
            currency,
            "MANUAL_OFFLINE",
            $"Manual settlement for session {session.Id}"
        );

        await _eventBus.PublishAsync(integrationEvent);
        await _repository.SaveChangesAsync(ct);
    }
}
