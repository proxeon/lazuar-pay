// apps/lazuar-api/Modules/Community/Application/Commands/ArchiveCouponCommand.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;

namespace Modules.Community.Application.Commands;

public record ArchiveCouponCommand(Guid OrganizationId, Guid CouponId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public class ArchiveCouponCommandHandler : ICommandHandler<ArchiveCouponCommand>
{
    private readonly ICommunityCouponRepository _repository;

    public ArchiveCouponCommandHandler(ICommunityCouponRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(ArchiveCouponCommand request, CancellationToken ct)
    {
        var coupon = await _repository.GetByIdAsync(request.CouponId, ct);
        
        if (coupon == null || coupon.OrganizationId != request.OrganizationId)
            throw new InvalidOperationException("Coupon not found.");

        coupon.Archive();
        
        _repository.Update(coupon);
        await _repository.SaveChangesAsync(ct);
    }
}
