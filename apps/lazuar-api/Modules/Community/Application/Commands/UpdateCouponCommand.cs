// apps/lazuar-api/Modules/Community/Application/Commands/UpdateCouponCommand.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;

namespace Modules.Community.Application.Commands;

public record UpdateCouponCommand(
    Guid OrganizationId,
    Guid CouponId,
    int MaxUses,
    decimal MinimumOriginalPrice,
    DateTime? ExpiresAt) : ICommand
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

        coupon.UpdateLimits(request.MaxUses, request.MinimumOriginalPrice, request.ExpiresAt);
        
        _repository.Update(coupon);
        await _repository.SaveChangesAsync(ct);
    }
}
