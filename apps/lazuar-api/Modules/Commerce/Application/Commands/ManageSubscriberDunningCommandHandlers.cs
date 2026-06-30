using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Commerce.Contracts.Commands;

namespace Modules.Commerce.Application.Commands;

public class PauseSubscriberDunningCommandHandler : ICommandHandler<PauseSubscriberDunningCommand>
{
    private readonly ICommerceRepository _repository;

    public PauseSubscriberDunningCommandHandler(ICommerceRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(PauseSubscriberDunningCommand request, CancellationToken ct)
    {
        var subscription = await _repository.GetSubscriptionByIdAsync(request.SubscriptionId, ct);
        if (subscription == null || subscription.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("Subscription not found.");
        }

        subscription.PauseDunning(request.PauseUntil);
        await _repository.SaveChangesAsync(ct);
    }
}

public class ResumeSubscriberDunningCommandHandler : ICommandHandler<ResumeSubscriberDunningCommand>
{
    private readonly ICommerceRepository _repository;

    public ResumeSubscriberDunningCommandHandler(ICommerceRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(ResumeSubscriberDunningCommand request, CancellationToken ct)
    {
        var subscription = await _repository.GetSubscriptionByIdAsync(request.SubscriptionId, ct);
        if (subscription == null || subscription.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("Subscription not found.");
        }

        subscription.ResumeDunning();
        await _repository.SaveChangesAsync(ct);
    }
}
