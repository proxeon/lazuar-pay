using System;
using System.Collections.Generic;
using BuildingBlocks.Application;

namespace Modules.Community.Contracts;

public record ZeroAmountCheckoutCompletedIntegrationEvent(
    Guid OrganizationId,
    Guid SubscriptionId,
    Guid ClientProfileId,
    decimal OriginalAmount,
    decimal DiscountAmount,
    string Currency,
    string CouponCode,
    Dictionary<string, string> Metadata) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
