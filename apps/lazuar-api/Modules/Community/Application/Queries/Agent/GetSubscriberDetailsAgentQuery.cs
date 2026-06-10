using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.CRM.Contracts;

namespace Modules.Community.Application.Queries.Agent;

[AgentTool("Fetch the complete profile, billing status, and plan details of a single subscriber using their exact SubscriptionId.", "low", "SUPER_ADMIN", "ADMIN")]
public record GetSubscriberDetailsAgentQuery(Guid OrganizationId, Guid SubscriptionId) : IQuery<AgentSubscriberDetailsResult>;

public record AgentSubscriberDetailsResult(
    string SubscriptionId,
    string Status,
    string PlanName,
    decimal PlanPrice,
    string Interval,
    DateTime? NextBillingDate,
    DateTime? CurrentPeriodEnd,
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    string? AdminNotes,
    bool IsReminderOnly);

public class GetSubscriberDetailsAgentQueryHandler : IQueryHandler<GetSubscriberDetailsAgentQuery, AgentSubscriberDetailsResult>
{
    private readonly ICommunitySubscriptionRepository _subscriptionRepository;
    private readonly ICommunityPlanRepository _planRepository;
    private readonly ICrmQueryService _crmQueryService;

    public GetSubscriberDetailsAgentQueryHandler(
        ICommunitySubscriptionRepository subscriptionRepository,
        ICommunityPlanRepository planRepository,
        ICrmQueryService crmQueryService)
    {
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
        _crmQueryService = crmQueryService;
    }

    public async Task<AgentSubscriberDetailsResult> Handle(GetSubscriberDetailsAgentQuery request, CancellationToken cancellationToken)
    {
        var subscription = await _subscriptionRepository.GetByIdAsync(request.SubscriptionId, cancellationToken);
        if (subscription == null || subscription.OrganizationId != request.OrganizationId)
        {
            throw new InvalidOperationException("Subscription not found in the current workspace.");
        }

        var plan = await _planRepository.GetByIdAsync(subscription.PlanId, cancellationToken);
        if (plan == null)
        {
            throw new InvalidOperationException("Associated plan not found.");
        }

        var profile = await _crmQueryService.GetClientProfileAsync(subscription.ClientProfileId);
        if (profile == null)
        {
            throw new InvalidOperationException("Associated customer profile not found.");
        }

        return new AgentSubscriberDetailsResult(
            subscription.Id.ToString(),
            subscription.Status,
            plan.Name,
            plan.Price,
            plan.Interval,
            subscription.NextRenewalDate,
            subscription.CurrentPeriodEnd,
            profile.FullName,
            profile.Email,
            profile.Phone,
            subscription.AdminNotes,
            subscription.IsReminderOnly
        );
    }
}
