// apps/lazuar-api/Modules/Community/Application/Commands/SendOneOffReminderCommand.cs
using BuildingBlocks.Application;

namespace Modules.Community.Application.Commands;

[AgentTool("Instantly send a custom message to a specific subscriber.", "medium", "SUPER_ADMIN", "ADMIN")]
public record SendOneOffReminderCommand(
    Guid OrganizationId,
    Guid SubscriptionId,
    Guid? TemplateId,
    string? CustomMessage,
    string Channel) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class SendOneOffReminderCommandHandler : ICommandHandler<SendOneOffReminderCommand>
{
    private readonly ICommunitySubscriptionRepository _repository;

    public SendOneOffReminderCommandHandler(ICommunitySubscriptionRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(SendOneOffReminderCommand request, CancellationToken ct)
    {
        var subscription = await _repository.GetByIdAsync(request.SubscriptionId, ct);
        if (subscription == null || subscription.OrganizationId != request.OrganizationId)
            throw new InvalidOperationException("Subscription not found.");

        subscription.SendOneOffReminder(request.TemplateId, request.CustomMessage, request.Channel);

        await _repository.SaveChangesAsync(ct);
    }
}
