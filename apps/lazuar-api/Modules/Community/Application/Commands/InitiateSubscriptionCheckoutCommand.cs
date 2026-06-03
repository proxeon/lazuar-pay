using BuildingBlocks.Application;

namespace Modules.Community.Application.Commands;

public record InitiateSubscriptionCheckoutCommand(
    Guid OrganizationId, 
    Guid SubscriptionId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class InitiateSubscriptionCheckoutCommandHandler : ICommandHandler<InitiateSubscriptionCheckoutCommand>
{
    private readonly ICommunitySubscriptionRepository _repository;

    public InitiateSubscriptionCheckoutCommandHandler(ICommunitySubscriptionRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(InitiateSubscriptionCheckoutCommand request, CancellationToken ct)
    {
        var subscription = await _repository.GetByIdAsync(request.SubscriptionId, ct);
        
        if (subscription == null || subscription.OrganizationId != request.OrganizationId)
            throw new InvalidOperationException("Subscription not found.");

        subscription.InitiateCheckout();

        await _repository.SaveChangesAsync(ct);
    }
}
