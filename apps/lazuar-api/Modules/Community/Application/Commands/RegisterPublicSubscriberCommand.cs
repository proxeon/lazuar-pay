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
    string TenantSlug,
    string PlanSlug,
    string Name,
    string Email,
    string Phone,
    Guid? GlobalUserId,
    string? CouponCode = null) : ICommand<string>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class RegisterPublicSubscriberCommandHandler : ICommandHandler<RegisterPublicSubscriberCommand, string>
{
    private readonly ICommunityPlanRepository _planRepository;
    private readonly ICommunitySubscriptionRepository _subscriptionRepository;
    private readonly ICommunityCouponRepository _couponRepository;
    private readonly ICommunityLinkService _linkService;
    private readonly IMediator _mediator;

    public RegisterPublicSubscriberCommandHandler(
        ICommunityPlanRepository planRepository,
        ICommunitySubscriptionRepository subscriptionRepository,
        ICommunityCouponRepository couponRepository,
        ICommunityLinkService linkService,
        IMediator mediator)
    {
        _planRepository = planRepository;
        _subscriptionRepository = subscriptionRepository;
        _couponRepository = couponRepository;
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

        decimal finalPrice = plan.Price;
        CommunityCoupon? appliedCoupon = null;

        if (!string.IsNullOrWhiteSpace(request.CouponCode))
        {
            var coupon = await _couponRepository.GetByCodeAsync(request.OrganizationId, request.CouponCode, ct);
            if (coupon == null) throw new InvalidOperationException("Invalid coupon code.");

            coupon.Validate(plan.Price);
            coupon.Reserve();

            subscription.SetPendingCoupon(coupon.Id);
            appliedCoupon = coupon;

            _couponRepository.Update(coupon);
            finalPrice = plan.Price - coupon.CalculateDiscount(plan.Price);
            if (finalPrice < 0) finalPrice = 0;
        }

        var baseUrl = _linkService.GetCommunityBaseUrl();
        var successUrl = $"{baseUrl}/{request.TenantSlug}/{plan.Slug}/success";
        var cancelUrl = $"{baseUrl}/{request.TenantSlug}/{plan.Slug}/checkout?cancelled=true";

        if (finalPrice <= 0 && appliedCoupon != null)
        {
            var periodStart = DateTime.UtcNow;
            var periodEnd = periodStart.AddDays(plan.Interval == "yr" ? 365 : 30);

            subscription.Activate(
                periodStart,
                periodEnd,
                0m,
                "MYR",
                "COUPON_100_OFF",
                null,
                "SYSTEM");

            appliedCoupon.ConfirmReservation();
            subscription.ClearPendingCoupon();

            await _subscriptionRepository.SaveChangesAsync(ct);
            return successUrl;
        }

        var metadata = new Dictionary<string, string>
        {
            ["type"] = "community_subscription",
            ["subscription_id"] = subscription.Id.ToString()
        };

        var checkoutQuery = new GenerateCheckoutSessionQuery(
            request.OrganizationId,
            finalPrice,
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
