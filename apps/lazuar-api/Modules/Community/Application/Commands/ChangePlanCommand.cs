// apps/lazuar-api/Modules/Community/Application/Commands/ChangePlanCommand.cs
using BuildingBlocks.Application;

namespace Modules.Community.Application.Commands;

[AgentTool("Change or upgrade a user's subscription plan.", "medium", "SUPER_ADMIN", "ADMIN")]
public record ChangePlanCommand(Guid OrganizationId, Guid SubscriptionId, Guid NewPlanId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class ChangePlanCommandHandler : ICommandHandler<ChangePlanCommand>
{
    private readonly ICommunitySubscriptionRepository _subscriptionRepository;
    private readonly ICommunityPlanRepository _planRepository;

    public ChangePlanCommandHandler(
        ICommunitySubscriptionRepository subscriptionRepository, 
        ICommunityPlanRepository planRepository)
    {
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
    }

    public async Task Handle(ChangePlanCommand request, CancellationToken ct)
    {
        var subscription = await _subscriptionRepository.GetByIdAsync(request.SubscriptionId, ct);
        
        if (subscription == null || subscription.OrganizationId != request.OrganizationId)
            throw new InvalidOperationException("Subscription not found.");

        if (subscription.PlanId == request.NewPlanId)
            throw new InvalidOperationException("Subscription is already on this plan.");

        var newPlan = await _planRepository.GetByIdAsync(request.NewPlanId, ct);
        if (newPlan == null || newPlan.OrganizationId != request.OrganizationId || !newPlan.IsActive)
            throw new InvalidOperationException("The requested plan is invalid or inactive.");

        subscription.SchedulePlanChange(request.NewPlanId);

        await _subscriptionRepository.SaveChangesAsync(ct);
    }
}
