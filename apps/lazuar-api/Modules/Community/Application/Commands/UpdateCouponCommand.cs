using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;

namespace Modules.Community.Application.Commands;

public record UpdateCouponCommand(
    Guid OrganizationId,
    Guid CouponId,
    string? Code,
    string? DiscountType,
    decimal? Amount,
    int MaxUses,
    decimal MinimumOriginalPrice,
    DateTime? ExpiresAt,
    bool? IsActive,
    IEnumerable<Guid>? ApplicablePlanIds = null) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class UpdateCouponCommandHandler : ICommandHandler<UpdateCouponCommand>
{
    private readonly ICommunityCouponRepository _repository;

    public UpdateCouponCommandHandler(ICommunityCouponRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(UpdateCouponCommand request, CancellationToken ct)
    {
        var coupon = await _repository.GetByIdAsync(request.CouponId, ct);
        
        if (coupon == null || coupon.OrganizationId != request.OrganizationId)
            throw new InvalidOperationException("Coupon not found.");

        coupon.UpdateDetails(
            request.Code ?? coupon.Code,
            request.DiscountType ?? coupon.DiscountType,
            request.Amount ?? coupon.Amount,
            request.MaxUses, 
            request.MinimumOriginalPrice, 
            request.ExpiresAt, 
            request.ApplicablePlanIds);

        if (request.IsActive.HasValue)
        {
            if (request.IsActive.Value) coupon.Restore();
            else coupon.Archive();
        }
        
        _repository.Update(coupon);
        await _repository.SaveChangesAsync(ct);
    }
}
