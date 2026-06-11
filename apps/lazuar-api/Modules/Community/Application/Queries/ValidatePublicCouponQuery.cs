using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using Modules.Community.Domain.Aggregates;

namespace Modules.Community.Application.Queries;

public record ValidatePublicCouponQuery(
    Guid OrganizationId,
    Guid PlanId,
    string Code) : IQuery<ValidatePublicCouponResult>;

public record ValidatePublicCouponResult(
    bool IsValid,
    decimal DiscountAmount,
    decimal FinalPrice,
    string? ErrorMessage);

public class ValidatePublicCouponQueryHandler : IQueryHandler<ValidatePublicCouponQuery, ValidatePublicCouponResult>
{
    private readonly ICommunityPlanRepository _planRepository;
    private readonly ICommunityCouponRepository _couponRepository;

    public ValidatePublicCouponQueryHandler(
        ICommunityPlanRepository planRepository,
        ICommunityCouponRepository couponRepository)
    {
        _planRepository = planRepository;
        _couponRepository = couponRepository;
    }

    public async Task<ValidatePublicCouponResult> Handle(ValidatePublicCouponQuery request, CancellationToken cancellationToken)
    {
        var plan = await _planRepository.GetByIdAsync(request.PlanId, cancellationToken);
        if (plan == null || plan.OrganizationId != request.OrganizationId || !plan.IsActive)
        {
            return new ValidatePublicCouponResult(false, 0, 0, "The requested subscription program is unavailable.");
        }

        var coupon = await _couponRepository.GetByCodeAsync(request.OrganizationId, request.Code, cancellationToken);
        if (coupon == null)
        {
            return new ValidatePublicCouponResult(false, 0, plan.Price, "Invalid coupon code.");
        }

        try
        {
            coupon.Validate(plan.Price);
            var discount = coupon.CalculateDiscount(plan.Price);
            var finalPrice = plan.Price - discount;
            if (finalPrice < 0) finalPrice = 0;

            return new ValidatePublicCouponResult(true, discount, finalPrice, null);
        }
        catch (BusinessRuleValidationException ex)
        {
            return new ValidatePublicCouponResult(false, 0, plan.Price, ex.Message);
        }
    }
}
