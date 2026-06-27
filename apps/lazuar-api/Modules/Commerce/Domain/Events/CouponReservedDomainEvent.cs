using System;
using BuildingBlocks.Domain;

namespace Modules.Commerce.Domain.Events;

public record CouponReservedDomainEvent(
    Guid CouponId,
    Guid OrganizationId,
    string Code) : IDomainEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
