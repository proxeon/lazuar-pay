using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Modules.Payments.Contracts.Queries;
using Modules.CRM.Contracts;
using Modules.Community.Domain.Aggregates;

namespace Modules.Community.Application.Commands;

public record InitiateSubscriptionCheckoutCommand(
    Guid OrganizationId,
    Guid SubscriptionId,
    string SuccessUrl,
    string CancelUrl,
    string? CouponCode = null) : ICommand<string>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class InitiateSubscriptionCheckoutCommandHandler : ICommandHandler<InitiateSubscriptionCheckoutCommand, string>
{
    private readonly ICommunitySubscriptionRepository _repository;
    private readonly ICommunityPlanRepository _planRepository;
    private readonly ICommunityCouponRepository _couponRepository;
    private readonly ICrmQueryService _crmQueryService;
    private readonly IMediator _mediator;

    public InitiateSubscriptionCheckoutCommandHandler(
        ICommunitySubscriptionRepository repository,
        ICommunityPlanRepository planRepository,
        ICommunityCouponRepository couponRepository,
        ICrmQueryService crmQueryService,
        IMediator mediator)
    {
        _repository = repository;
        _planRepository = planRepository;
        _couponRepository = couponRepository;
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

        subscription.InitiateCheckout();

        decimal finalPrice = plan.Price;
        CommunityCoupon? appliedCoupon = null;

        if (!string.IsNullOrWhiteSpace(request.CouponCode))
        {
            var coupon = await _couponRepository.GetByCodeAsync(request.OrganizationId, request.CouponCode, ct);
            if (coupon == null) throw new InvalidOperationException("Invalid coupon code.");

            coupon.Validate(plan.Price, plan.Id);
            coupon.Reserve();
            subscription.SetPendingCoupon(coupon.Id);
            appliedCoupon = coupon;

            _couponRepository.Update(coupon);
            finalPrice = plan.Price - coupon.CalculateDiscount(plan.Price);
            if (finalPrice < 0) finalPrice = 0;
        }

        await _repository.SaveChangesAsync(ct);

        var successUrlWithContext = AppendQueryParameter(request.SuccessUrl, "sub_id", subscription.Id.ToString());
        var cancelUrlWithContext = AppendQueryParameter(request.CancelUrl, "sub_id", subscription.Id.ToString());

        if (finalPrice <= 0 && appliedCoupon != null)
        {
            var periodStart = DateTime.UtcNow;
            var intervalDays = plan.Interval == "yr" ? 365 : 30;
            var periodEnd = periodStart.AddDays(intervalDays);

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

            await _repository.SaveChangesAsync(ct);
            return successUrlWithContext;
        }

        var customerProfile = await _crmQueryService.GetClientProfileAsync(subscription.ClientProfileId);

        var metadata = new Dictionary<string, string>
        {
            ["type"] = "community_subscription",
            ["subscription_id"] = subscription.Id.ToString(),
            ["customer_name"] = customerProfile?.Full_name ?? "",
            ["customer_phone"] = customerProfile?.Phone ?? ""
        };

        var query = new GenerateCheckoutSessionQuery(
            request.OrganizationId,
            finalPrice,
            "MYR",
            plan.Name,
            customerProfile?.Email ?? "",
            successUrlWithContext,
            cancelUrlWithContext,
            metadata,
            SetupFutureUsage: true);

        var checkoutUrl = await _mediator.Send(query, ct);
        return checkoutUrl;
    }

    private static string AppendQueryParameter(string url, string key, string value)
    {
        var uriBuilder = new UriBuilder(url);
        var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
        query[key] = value;
        uriBuilder.Query = query.ToString();
        return uriBuilder.ToString();
    }
}
