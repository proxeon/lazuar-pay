using BuildingBlocks.Application;

namespace Modules.Community.Application.Commands;

public record ExtendGracePeriodCommand(Guid OrganizationId, Guid SubscriptionId, int Days) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class ExtendGracePeriodCommandHandler : ICommandHandler<ExtendGracePeriodCommand>
{
    private readonly ICommunitySubscriptionRepository _repository;

    public ExtendGracePeriodCommandHandler(ICommunitySubscriptionRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(ExtendGracePeriodCommand request, CancellationToken ct)
    {
        var subscription = await _repository.GetByIdAsync(request.SubscriptionId, ct);
        if (subscription == null || subscription.OrganizationId != request.OrganizationId)
            throw new InvalidOperationException("Subscription not found.");

        subscription.ExtendGracePeriod(request.Days);

        await _repository.SaveChangesAsync(ct);
    }
}
