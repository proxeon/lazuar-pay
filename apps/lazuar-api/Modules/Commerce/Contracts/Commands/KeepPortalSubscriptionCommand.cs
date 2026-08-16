using System;
using BuildingBlocks.Application;

namespace Modules.Commerce.Contracts.Commands;

public record KeepPortalSubscriptionCommand(
    string TenantSlug,
    string Token,
    Guid SubscriptionId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
