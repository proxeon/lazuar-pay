using BuildingBlocks.Application;

namespace Modules.Community.Application.Commands;

public record ArchivePlanCommand(Guid OrganizationId, Guid PlanId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class ArchivePlanCommandHandler : ICommandHandler<ArchivePlanCommand>
{
    private readonly ICommunityPlanRepository _planRepository;
    private readonly ICommunitySubscriptionRepository _uow;

    public ArchivePlanCommandHandler(ICommunityPlanRepository planRepository, ICommunitySubscriptionRepository uow)
    {
        _planRepository = planRepository;
        _uow = uow;
    }

    public async Task Handle(ArchivePlanCommand request, CancellationToken ct)
    {
        var plan = await _planRepository.GetByIdAsync(request.PlanId, ct);
        if (plan == null || plan.OrganizationId != request.OrganizationId)
            throw new InvalidOperationException("Plan not found.");

        plan.Archive();

        await _uow.SaveChangesAsync(ct);
    }
}
