using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using MediatR;
using Modules.Payments.Contracts.Queries;
using Modules.CRM.Contracts;

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
        if (!string.IsNullOrWhiteSpace(request.CouponCode))
        {
            var coupon = await _couponRepository.GetByCodeAsync(request.OrganizationId, request.CouponCode, ct);
            if (coupon == null) throw new InvalidOperationException("Invalid coupon code.");
            
            coupon.Validate(plan.Price);
            coupon.Reserve();
            subscription.SetPendingCoupon(coupon.Id);
            
            _couponRepository.Update(coupon);
            finalPrice = plan.Price - coupon.CalculateDiscount(plan.Price);
            if (finalPrice < 0) finalPrice = 0;
        }

        await _repository.SaveChangesAsync(ct);

        var customerProfile = await _crmQueryService.GetClientProfileAsync(subscription.ClientProfileId);
        var customerEmail = customerProfile?.Email ?? "";

        var metadata = new Dictionary<string, string>
        {
            ["type"] = "community_subscription",
            ["subscription_id"] = subscription.Id.ToString()
        };

        var query = new GenerateCheckoutSessionQuery(
            request.OrganizationId,
            finalPrice,
            "MYR",
            plan.Name,
            customerEmail,
            request.SuccessUrl,
            request.CancelUrl,
            metadata);

        var checkoutUrl = await _mediator.Send(query, ct);
        return checkoutUrl;
    }
}
