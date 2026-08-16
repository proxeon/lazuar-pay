using System;
using BuildingBlocks.Application;

namespace Modules.Commerce.Contracts.Commands;

/// <summary>
/// Ops PDPA wipe for the buyer on this subscription. Resolves the client profile,
/// scrubs commerce transaction-log PII, then runs <c>AnonymizeClientProfileCommand</c>.
/// Idempotent when the profile is already dummy.
/// </summary>
public record AnonymizeSubscriberCommand(Guid OrganizationId, Guid SubscriptionId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
