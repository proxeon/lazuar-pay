// apps/lazuar-api/Modules/Community/Application/Commands/PauseRemindersCommand.cs
using BuildingBlocks.Application;

namespace Modules.Community.Application.Commands;

[AgentTool("Halt automated dunning emails for a specific user until a specific date.", "medium", "SUPER_ADMIN", "ADMIN")]
public record PauseRemindersCommand(Guid OrganizationId, Guid SubscriptionId, DateTime? PauseUntil) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class PauseRemindersCommandHandler : ICommandHandler<PauseRemindersCommand>
{
    private readonly ICommunitySubscriptionRepository _repository;

    public PauseRemindersCommandHandler(ICommunitySubscriptionRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(PauseRemindersCommand request, CancellationToken ct)
    {
        var subscription = await _repository.GetByIdAsync(request.SubscriptionId, ct);
        if (subscription == null || subscription.OrganizationId != request.OrganizationId)
            throw new InvalidOperationException("Subscription not found.");

        subscription.PauseReminders(request.PauseUntil);

        await _repository.SaveChangesAsync(ct);
    }
}
