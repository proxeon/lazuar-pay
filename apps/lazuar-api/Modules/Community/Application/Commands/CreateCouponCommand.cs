using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Community.Domain.Aggregates;

namespace Modules.Community.Application.Commands;

public enum DiscountType
{
    FIXED,
    PERCENTAGE
}

[AgentTool("Create a new promotional coupon code for discounts.", "COMMUNITY", "medium", "SUPER_ADMIN", "ADMIN")]
public record CreateCouponCommand(
    Guid OrganizationId,
    [property: Description("e.g. SUMMER20")] string Code,
    DiscountType DiscountType,
    [property: Description("e.g. 10.00")] decimal Amount,
    int MaxUses,
    DateTime? ExpiresAt,
    decimal MinimumOriginalPrice = 0,
    IEnumerable<Guid>? ApplicablePlanIds = null) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class CreateCouponCommandHandler : ICommandHandler<CreateCouponCommand, Guid>
{
    private readonly ICommunityCouponRepository _couponRepository;

    public CreateCouponCommandHandler(ICommunityCouponRepository couponRepository)
    {
        _couponRepository = couponRepository;
    }

    public async Task<Guid> Handle(CreateCouponCommand request, CancellationToken ct)
    {
        var coupon = new CommunityCoupon(
            request.OrganizationId,
            request.Code,
            request.DiscountType.ToString(),
            request.Amount,
            request.MaxUses,
            request.ExpiresAt,
            request.MinimumOriginalPrice,
            request.ApplicablePlanIds);

        _couponRepository.Add(coupon);
        await _couponRepository.SaveChangesAsync(ct);
        return coupon.Id;
    }
}
