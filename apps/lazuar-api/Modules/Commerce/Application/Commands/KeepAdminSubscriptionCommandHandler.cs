using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Commerce.Contracts.Commands;

namespace Modules.Commerce.Application.Commands;

public class KeepAdminSubscriptionCommandHandler : ICommandHandler<KeepAdminSubscriptionCommand>
{
    private readonly ICommerceRepository _repository;

    public KeepAdminSubscriptionCommandHandler(ICommerceRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(KeepAdminSubscriptionCommand request, CancellationToken ct)
    {
        var subscription = await _repository.GetSubscriptionByIdAsync(request.OrganizationId, request.SubscriptionId, ct);
        if (subscription == null || subscription.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("Subscription not found.");
        }

        if (subscription.Status == "CANCELED")
        {
            throw new InvalidOperationException("Subscription is already canceled.");
        }

        subscription.ClearScheduledCancel();
        await _repository.SaveChangesAsync(ct);
    }
}
