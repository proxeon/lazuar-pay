using System;
using BuildingBlocks.Application;

namespace Modules.Commerce.Contracts.Commands;

public record PauseSubscriberDunningCommand(Guid OrganizationId, Guid SubscriptionId, DateTime PauseUntil) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public record ResumeSubscriberDunningCommand(Guid OrganizationId, Guid SubscriptionId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
