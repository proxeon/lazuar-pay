using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;

namespace Modules.Community.Application.Commands;

[AgentTool("Reactivate a cancelled, expired, or banned subscription.", "high", "SUPER_ADMIN", "ADMIN")]
public record ReactivateSubscriptionCommand(Guid OrganizationId, Guid SubscriptionId, string RecordedBy) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class ReactivateSubscriptionCommandHandler : ICommandHandler<ReactivateSubscriptionCommand>
{
    private readonly ICommunitySubscriptionRepository _repository;

    public ReactivateSubscriptionCommandHandler(ICommunitySubscriptionRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(ReactivateSubscriptionCommand request, CancellationToken ct)
    {
        var subscription = await _repository.GetByIdAsync(request.SubscriptionId, ct);

        if (subscription == null || subscription.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("Subscription not found.");
        }

        subscription.Reactivate(request.RecordedBy);

        await _repository.SaveChangesAsync(ct);
    }
}
