using BuildingBlocks.Application;
using MediatR;
using Modules.Payments.Contracts.Queries;
using Modules.CRM.Contracts;

namespace Modules.Community.Application.Commands;

public record InitiateSubscriptionCheckoutCommand(
    Guid OrganizationId, 
    Guid SubscriptionId,
    string SuccessUrl,
    string CancelUrl) : ICommand<string>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class InitiateSubscriptionCheckoutCommandHandler : ICommandHandler<InitiateSubscriptionCheckoutCommand, string>
{
    private readonly ICommunitySubscriptionRepository _repository;
    private readonly ICommunityPlanRepository _planRepository;
    private readonly ICrmQueryService _crmQueryService;
    private readonly IMediator _mediator;

    public InitiateSubscriptionCheckoutCommandHandler(
        ICommunitySubscriptionRepository repository,
        ICommunityPlanRepository planRepository,
        ICrmQueryService crmQueryService,
        IMediator mediator)
    {
        _repository = repository;
        _planRepository = planRepository;
        _crmQueryService = crmQueryService;
        _mediator = mediator;
    }

    public async Task<string> Handle(InitiateSubscriptionCheckoutCommand request, CancellationToken ct)
    {
        var subscription = await _repository.GetByIdAsync(request.SubscriptionId, ct);
        
        if (subscription == null || subscription.OrganizationId != request.OrganizationId)
            throw new InvalidOperationException("Subscription not found.");

        var plan = await _planRepository.GetByIdAsync(subscription.PlanId, ct);
        if (plan == null)
            throw new InvalidOperationException("Plan not found.");

        // 1. Update Domain State (Drops Abandoned Cart Event to Outbox)
        subscription.InitiateCheckout();
        await _repository.SaveChangesAsync(ct);

        // 2. Fetch Customer Data via CRM Read Model (Cross-module query without DB Join)
        var customerProfile = await _crmQueryService.GetClientProfileAsync(subscription.ClientProfileId);
        var customerEmail = customerProfile?.Email ?? "";

        // 3. Cross-Module Query to get the Checkout URL synchronously
        var metadata = new Dictionary<string, string>
        {
            ["type"] = "community_subscription",
            ["subscription_id"] = subscription.Id.ToString()
        };

        var query = new GenerateCheckoutSessionQuery(
            request.OrganizationId,
            plan.Price,
            "MYR", // Currency can be hardcoded or fetched from Org settings
            plan.Name, // Pre-fill Product Name
            customerEmail, // Pre-fill Customer Email
            request.SuccessUrl,
            request.CancelUrl,
            metadata);

        // Ask the Payments module for the URL
        var checkoutUrl = await _mediator.Send(query, ct);

        return checkoutUrl;
    }
}
