using System;
using BuildingBlocks.Application;

namespace Modules.Commerce.Contracts.Commands;

/// <summary>
/// Ops-initiated subscription cancel (mirrors portal cancel without magic-token auth).
/// <paramref name="AtPeriodEnd"/> defaults false so existing ops callers stay immediate.
/// Returns <c>CANCELED</c> or <c>scheduled</c>.
/// </summary>
public record CancelAdminSubscriptionCommand(
    Guid OrganizationId,
    Guid SubscriptionId,
    bool AtPeriodEnd = false) : ICommand<string>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
