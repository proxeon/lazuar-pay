using System;
using BuildingBlocks.Application;

namespace Modules.Commerce.Contracts.Commands;

/// <summary>
/// Ops-initiated subscription cancel (mirrors portal cancel without magic-token auth).
/// </summary>
public record CancelAdminSubscriptionCommand(
    Guid OrganizationId,
    Guid SubscriptionId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
