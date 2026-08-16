using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Commerce.Contracts.Commands;

namespace Modules.Commerce.Application.Commands;

public class CancelAdminSubscriptionCommandHandler : ICommandHandler<CancelAdminSubscriptionCommand, string>
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

    public async Task<string> Handle(CancelAdminSubscriptionCommand request, CancellationToken ct)
    {
        var subscription = await _repository.GetSubscriptionByIdAsync(request.SubscriptionId, ct);
        if (subscription == null || subscription.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("Subscription not found.");
        }

        return await SubscriptionCancelApplier.ApplyAndPersistAsync(
            _repository,
            _eventBus,
            subscription,
            request.AtPeriodEnd,
            canceledStatus: "CANCELED",
            ct);
    }
}
