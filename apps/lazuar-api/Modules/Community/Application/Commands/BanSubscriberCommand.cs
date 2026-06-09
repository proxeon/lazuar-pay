// apps/lazuar-api/Modules/Community/Application/Commands/BanSubscriberCommand.cs
using BuildingBlocks.Application;

namespace Modules.Community.Application.Commands;

[AgentTool("Immediately revoke access and terminate a subscription.", "high", "SUPER_ADMIN", "ADMIN")]
public record BanSubscriberCommand(Guid OrganizationId, Guid SubscriptionId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class BanSubscriberCommandHandler : ICommandHandler<BanSubscriberCommand>
{
    private readonly ICommunitySubscriptionRepository _repository;

    public BanSubscriberCommandHandler(ICommunitySubscriptionRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(BanSubscriberCommand request, CancellationToken ct)
    {
        var subscription = await _repository.GetByIdAsync(request.SubscriptionId, ct);
        
        if (subscription == null || subscription.OrganizationId != request.OrganizationId)
            throw new InvalidOperationException("Subscription not found.");

        subscription.Ban();

        await _repository.SaveChangesAsync(ct);
    }
}
