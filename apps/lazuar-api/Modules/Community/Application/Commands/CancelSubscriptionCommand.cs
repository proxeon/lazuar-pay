// apps/lazuar-api/Modules/Community/Application/Commands/CancelSubscriptionCommand.cs
using BuildingBlocks.Application;

namespace Modules.Community.Application.Commands;

[AgentTool("Cancel a user's subscription at the end of their billing cycle.", "COMMUNITY", "medium", "SUPER_ADMIN", "ADMIN")]
public record CancelSubscriptionCommand(
    Guid OrganizationId,
    Guid SubscriptionId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class CancelSubscriptionCommandHandler : ICommandHandler<CancelSubscriptionCommand>
{
    private readonly ICommunitySubscriptionRepository _repository;

    public CancelSubscriptionCommandHandler(ICommunitySubscriptionRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(CancelSubscriptionCommand request, CancellationToken ct)
    {
        var subscription = await _repository.GetByIdAsync(request.SubscriptionId, ct);

        if (subscription == null || subscription.OrganizationId != request.OrganizationId)
            throw new InvalidOperationException("Subscription not found.");

        subscription.Cancel();

        await _repository.SaveChangesAsync(ct);
    }
}
