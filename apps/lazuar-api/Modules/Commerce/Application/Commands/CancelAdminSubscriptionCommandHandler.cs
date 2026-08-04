using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Contracts.Events;

namespace Modules.Commerce.Application.Commands;

public class CancelAdminSubscriptionCommandHandler : ICommandHandler<CancelAdminSubscriptionCommand>
{
    private readonly ICommerceRepository _repository;
    private readonly IEventBus _eventBus;

    public CancelAdminSubscriptionCommandHandler(
        ICommerceRepository repository,
        [FromKeyedServices("CommerceEventBus")] IEventBus eventBus)
    {
        _repository = repository;
        _eventBus = eventBus;
    }

    public async Task Handle(CancelAdminSubscriptionCommand request, CancellationToken ct)
    {
        var subscription = await _repository.GetSubscriptionByIdAsync(request.SubscriptionId, ct);
        if (subscription == null || subscription.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("Subscription not found.");
        }

        if (subscription.Status == "CANCELED")
        {
            return;
        }

        if (subscription.Status is not ("ACTIVE" or "PAST_DUE" or "SUSPENDED"))
        {
            throw new InvalidOperationException($"Subscription cannot be canceled from status '{subscription.Status}'.");
        }

        subscription.Cancel();

        var product = await _repository.GetProductByIdAsync(subscription.ProductId, ct);
        var fulfillmentTargets = product?.FulfillmentTargets.ToList() ?? [];

        await _eventBus.PublishAsync(new SubscriptionCanceledIntegrationEvent(
            subscription.OrganizationId,
            subscription.Id,
            subscription.ClientProfileId,
            subscription.ProductId,
            fulfillmentTargets));

        await _repository.SaveChangesAsync(ct);
    }
}
