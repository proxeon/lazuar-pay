using BuildingBlocks.Application;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Modules.Community.Application.Commands;

[AgentTool("Resend the welcome email and onboarding links to a subscriber.", "low", "SUPER_ADMIN", "ADMIN")]
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
        {
            throw new InvalidOperationException("Subscription not found.");
        }

        subscription.ReplayActivationEvent();

        await _repository.SaveChangesAsync(ct);
    }
}
