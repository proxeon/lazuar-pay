using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Modules.Community.Domain.Aggregates;
using Modules.CRM.Contracts;
using Modules.Payments.Contracts.Queries;

namespace Modules.Community.Application.Commands;

public record RegisterPublicSubscriberCommand(
    Guid OrganizationId,
    string PlanSlug,
    string Name,
    string Email,
    string Phone,
    Guid? GlobalUserId) : ICommand<string>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class RegisterPublicSubscriberCommandHandler : ICommandHandler<RegisterPublicSubscriberCommand, string>
{
    private readonly ICommunityPlanRepository _planRepository;
    private readonly ICommunitySubscriptionRepository _subscriptionRepository;
    private readonly ICommunityLinkService _linkService;
    private readonly IMediator _mediator;

    public RegisterPublicSubscriberCommandHandler(
        ICommunityPlanRepository planRepository,
        ICommunitySubscriptionRepository subscriptionRepository,
        ICommunityLinkService linkService,
        IMediator mediator)
    {
        _planRepository = planRepository;
        _subscriptionRepository = subscriptionRepository;
        _linkService = linkService;
        _mediator = mediator;
    }

    public async Task<string> Handle(RegisterPublicSubscriberCommand request, CancellationToken ct)
    {
        var plan = await _planRepository.GetBySlugAsync(request.OrganizationId, request.PlanSlug, ct);
        if (plan == null || !plan.IsActive)
        {
            throw new InvalidOperationException("The requested subscription program is unavailable.");
        }

        // Pass GlobalUserId down to the CRM module
        var profileCommand = new CreateClientProfileCommand(
            request.OrganizationId,
            request.Name,
            request.Email,
            request.Phone,
            request.GlobalUserId);

        var profileId = await _mediator.Send(profileCommand, ct);

        var subscription = new CommunitySubscription(
            request.OrganizationId,
            profileId,
            plan.Id,
            "ONLINE_CHECKOUT",
            isReminderOnly: false,
            preferredChannel: null);

        subscription.InitiateCheckout();
        _subscriptionRepository.Add(subscription);

        var baseUrl = _linkService.GetCommunityBaseUrl();
        var successUrl = $"{baseUrl}/{plan.Slug}/success";
        var cancelUrl = $"{baseUrl}/{plan.Slug}/checkout?cancelled=true";

        var metadata = new Dictionary<string, string>
        {
            ["type"] = "community_subscription",
            ["subscription_id"] = subscription.Id.ToString()
        };

        var checkoutQuery = new GenerateCheckoutSessionQuery(
            request.OrganizationId,
            plan.Price,
            "MYR",
            $"{plan.Name} (Monthly Subscription)",
            request.Email,
            successUrl,
            cancelUrl,
            metadata);

        var checkoutUrl = await _mediator.Send(checkoutQuery, ct);

        subscription.SetPaymentGatewaySessionId(checkoutUrl);
        await _subscriptionRepository.SaveChangesAsync(ct);

        return checkoutUrl;
    }
}
