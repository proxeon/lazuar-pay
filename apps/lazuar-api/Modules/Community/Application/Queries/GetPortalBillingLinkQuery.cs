using BuildingBlocks.Application;
using MediatR;
using Modules.CRM.Contracts;
using Modules.Payments.Contracts.Queries;

namespace Modules.Community.Application.Queries;

public record GetPortalBillingLinkQuery(Guid OrganizationId, Guid SubscriptionId, string BaseUrl) : IQuery<string>;

public class GetPortalBillingLinkQueryHandler : IQueryHandler<GetPortalBillingLinkQuery, string>
{
    private readonly ICommunitySubscriptionRepository _subscriptionRepository;
    private readonly ICommunityPlanRepository _planRepository;
    private readonly ICrmQueryService _crmQueryService;
    private readonly IMediator _mediator;

    public GetPortalBillingLinkQueryHandler(
        ICommunitySubscriptionRepository subscriptionRepository,
        ICommunityPlanRepository planRepository,
        ICrmQueryService crmQueryService,
        IMediator mediator)
    {
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
        _crmQueryService = crmQueryService;
        _mediator = mediator;
    }

    public async Task<string> Handle(GetPortalBillingLinkQuery request, CancellationToken ct)
    {
        var subscription = await _subscriptionRepository.GetByIdAsync(request.SubscriptionId, ct);
        if (subscription == null || subscription.OrganizationId != request.OrganizationId)
            throw new InvalidOperationException("Subscription not found.");

        if (subscription.IsReminderOnly)
            throw new InvalidOperationException("This subscription is managed manually and does not support automated billing portals.");

        var planId = subscription.PendingPlanId ?? subscription.PlanId;
        var plan = await _planRepository.GetByIdAsync(planId, ct);
        if (plan == null)
            throw new InvalidOperationException("Plan not found.");

        var customerProfile = await _crmQueryService.GetClientProfileAsync(subscription.ClientProfileId);
        var customerEmail = customerProfile?.Email ?? throw new InvalidOperationException("Customer profile email missing.");

        var metadata = new Dictionary<string, string>
        {
            ["type"] = "community_subscription",
            ["subscription_id"] = subscription.Id.ToString()
        };

        try
        {
            var portalQuery = new GenerateCustomerPortalQuery(request.OrganizationId, customerEmail, request.BaseUrl);
            var portalUrl = await _mediator.Send(portalQuery, ct);
            return portalUrl;
        }
        catch (InvalidOperationException)
        {
            var checkoutQuery = new GenerateCheckoutSessionQuery(
                request.OrganizationId,
                plan.Price,
                "MYR",
                plan.Name,
                customerEmail,
                request.BaseUrl,
                request.BaseUrl,
                metadata);

            return await _mediator.Send(checkoutQuery, ct);
        }
    }
}
