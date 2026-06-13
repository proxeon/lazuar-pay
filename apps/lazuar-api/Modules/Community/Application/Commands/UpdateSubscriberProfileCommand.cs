// apps/lazuar-api/Modules/Community/Application/Commands/UpdateSubscriberProfileCommand.cs
using BuildingBlocks.Application;

namespace Modules.Community.Application.Commands;

[AgentTool("Update internal admin notes, reminder settings, or change the preferred communication channel.", "COMMUNITY", "low", "SUPER_ADMIN", "ADMIN")]
public record UpdateSubscriberProfileCommand(
    Guid OrganizationId,
    Guid SubscriptionId,
    bool IsReminderOnly,
    string? PreferredChannel,
    string? AdminNotes,
    DateTime? NextRenewalDate) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class UpdateSubscriberProfileCommandHandler : ICommandHandler<UpdateSubscriberProfileCommand>
{
    private readonly ICommunitySubscriptionRepository _repository;

    public UpdateSubscriberProfileCommandHandler(ICommunitySubscriptionRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(UpdateSubscriberProfileCommand request, CancellationToken ct)
    {
        var subscription = await _repository.GetByIdAsync(request.SubscriptionId, ct);
        if (subscription == null || subscription.OrganizationId != request.OrganizationId)
            throw new InvalidOperationException("Subscription not found.");

        subscription.UpdateProfile(
            request.IsReminderOnly,
            request.PreferredChannel,
            request.AdminNotes,
            request.NextRenewalDate);

        await _repository.SaveChangesAsync(ct);
    }
}
