using BuildingBlocks.Application;

namespace Modules.Community.Application.Commands;

public record ResendOnboardingCommand(Guid OrganizationId, Guid SubscriptionId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class ResendOnboardingCommandHandler : ICommandHandler<ResendOnboardingCommand>
{
    private readonly ICommunitySubscriptionRepository _repository;

    public ResendOnboardingCommandHandler(ICommunitySubscriptionRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(ResendOnboardingCommand request, CancellationToken ct)
    {
        var subscription = await _repository.GetByIdAsync(request.SubscriptionId, ct);
        
        if (subscription == null || subscription.OrganizationId != request.OrganizationId)
            throw new InvalidOperationException("Subscription not found.");

        subscription.ReplayActivationEvent();

        await _repository.SaveChangesAsync(ct);
    }
}
