using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Domain.Aggregates;

namespace Modules.Commerce.Application.Commands;

public class CreateCouponCommandHandler : ICommandHandler<CreateCouponCommand, Guid>
{
    private readonly ICommerceRepository _repository;

    public CreateCouponCommandHandler(ICommerceRepository repository)
    {
        _repository = repository;
    }

    public async Task<Guid> Handle(CreateCouponCommand request, CancellationToken ct)
    {
        var coupon = new Coupon(
            request.OrganizationId,
            request.Code,
            request.DiscountType,
            request.Amount,
            request.MaxUses,
            request.ExpiresAt,
            request.MinimumOriginalPrice,
            request.ApplicableProductIds
        );

        _repository.AddCoupon(coupon);
        await _repository.SaveChangesAsync(ct);

        return coupon.Id;
    }
}

public class UpdateCouponCommandHandler : ICommandHandler<UpdateCouponCommand>
{
    private readonly ICommerceRepository _repository;

    public UpdateCouponCommandHandler(ICommerceRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(UpdateCouponCommand request, CancellationToken ct)
    {
        var coupon = await _repository.GetCouponByIdAsync(request.CouponId, ct);
        if (coupon == null || coupon.OrganizationId != request.OrganizationId)
            throw new InvalidOperationException("Coupon not found.");

        coupon.UpdateDetails(
            request.Code ?? coupon.Code,
            request.DiscountType ?? coupon.DiscountType,
            request.Amount ?? coupon.Amount,
            request.MaxUses ?? coupon.MaxUses,
            request.MinimumOriginalPrice ?? coupon.MinimumOriginalPrice,
            request.ExpiresAt,
            request.ApplicableProductIds
        );

        if (request.IsActive.HasValue)
        {
            if (request.IsActive.Value) coupon.Restore();
            else coupon.Archive();
        }

        await _repository.SaveChangesAsync(ct);
    }
}

public class DeleteCouponCommandHandler : ICommandHandler<DeleteCouponCommand>
{
    private readonly ICommerceRepository _repository;

    public DeleteCouponCommandHandler(ICommerceRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(DeleteCouponCommand request, CancellationToken ct)
    {
        var coupon = await _repository.GetCouponByIdAsync(request.CouponId, ct);
        if (coupon == null || coupon.OrganizationId != request.OrganizationId)
            throw new InvalidOperationException("Coupon not found.");

        coupon.Archive();
        await _repository.SaveChangesAsync(ct);
    }
}
