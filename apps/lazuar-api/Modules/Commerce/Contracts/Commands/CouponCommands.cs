using System;
using System.Collections.Generic;
using BuildingBlocks.Application;

namespace Modules.Commerce.Contracts.Commands;

public record CreateCouponCommand(
    Guid OrganizationId,
    string Code,
    string DiscountType,
    decimal Amount,
    int MaxUses,
    decimal MinimumOriginalPrice,
    DateTime? ExpiresAt,
    List<Guid> ApplicableProductIds) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public record UpdateCouponCommand(
    Guid OrganizationId,
    Guid CouponId,
    string? Code,
    string? DiscountType,
    decimal? Amount,
    int? MaxUses,
    decimal? MinimumOriginalPrice,
    DateTime? ExpiresAt,
    List<Guid>? ApplicableProductIds,
    bool? IsActive) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public record DeleteCouponCommand(Guid OrganizationId, Guid CouponId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
