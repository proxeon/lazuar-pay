using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;

namespace Modules.Community.Application.Commands;

public record TransitionSubscriptionToPastDueCommand(Guid OrganizationId, Guid SubscriptionId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class TransitionSubscriptionToPastDueCommandHandler : ICommandHandler<TransitionSubscriptionToPastDueCommand>
{
    private readonly ICommunitySubscriptionRepository _repository;

    public TransitionSubscriptionToPastDueCommandHandler(ICommunitySubscriptionRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(TransitionSubscriptionToPastDueCommand request, CancellationToken ct)
    {
        var subscription = await _repository.GetByIdAsync(request.SubscriptionId, ct);
        if (subscription == null || subscription.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("Subscription not found.");
        }

        subscription.MarkAsPastDue();

        await _repository.SaveChangesAsync(ct);
    }
}
