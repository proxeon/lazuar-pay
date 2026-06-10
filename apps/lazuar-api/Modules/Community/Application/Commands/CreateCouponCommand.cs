using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Community.Domain.Aggregates;

namespace Modules.Community.Application.Commands;

[AgentTool("Create a new promotional coupon code for discounts.", "medium", "SUPER_ADMIN", "ADMIN")]
public record CreateCouponCommand(
    Guid OrganizationId,
    string Code,
    string DiscountType,
    decimal Amount,
    int MaxUses,
    DateTime? ExpiresAt) : ICommand<Guid>
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
            request.DiscountType,
            request.Amount,
            request.MaxUses,
            request.ExpiresAt);

        _couponRepository.Add(coupon);
        await _couponRepository.SaveChangesAsync(ct);
        return coupon.Id;
    }
}
