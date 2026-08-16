using System;
using BuildingBlocks.Application;

namespace Modules.Commerce.Contracts.Commands;

public record KeepAdminSubscriptionCommand(
    Guid OrganizationId,
    Guid SubscriptionId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
