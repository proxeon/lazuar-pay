// apps/lazuar-api/Modules/Community/Application/Commands/ScheduleOneOffReminderCommand.cs
using BuildingBlocks.Application;

namespace Modules.Community.Application.Commands;

[AgentTool("Schedule a message to be sent to a user in the future.", "medium", "SUPER_ADMIN", "ADMIN")]
public record ScheduleOneOffReminderCommand(
    Guid OrganizationId,
    Guid SubscriptionId,
    Guid? TemplateId,
    string? CustomMessage,
    string Channel,
    DateTime ScheduledAt) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class ScheduleOneOffReminderCommandHandler : ICommandHandler<ScheduleOneOffReminderCommand>
{
    private readonly ICommunitySubscriptionRepository _repository;

    public ScheduleOneOffReminderCommandHandler(ICommunitySubscriptionRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(ScheduleOneOffReminderCommand request, CancellationToken ct)
    {
        var subscription = await _repository.GetByIdAsync(request.SubscriptionId, ct);
        if (subscription == null || subscription.OrganizationId != request.OrganizationId)
            throw new InvalidOperationException("Subscription not found.");

        subscription.ScheduleOneOffReminder(request.TemplateId, request.CustomMessage, request.Channel, request.ScheduledAt);

        await _repository.SaveChangesAsync(ct);
    }
}
