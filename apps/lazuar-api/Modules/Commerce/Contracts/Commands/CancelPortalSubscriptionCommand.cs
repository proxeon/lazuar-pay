using System;
using BuildingBlocks.Application;

namespace Modules.Commerce.Contracts.Commands;

/// <summary>
/// Portal cancel. <paramref name="AtPeriodEnd"/> defaults true (healthy ACTIVE stays live until paid-through).
/// Returns <c>canceled</c> or <c>scheduled</c>.
/// </summary>
public record CancelPortalSubscriptionCommand(
    string TenantSlug,
    string Token,
    Guid SubscriptionId,
    bool AtPeriodEnd = true) : ICommand<string>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
