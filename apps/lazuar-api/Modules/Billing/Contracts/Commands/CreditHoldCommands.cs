using System;
using BuildingBlocks.Application;

namespace Modules.Billing.Contracts.Commands;

/// <summary>Reserve credits into a <c>CreditHold</c> for a multi-unit operation. Returns the hold id.</summary>
public record ReserveCreditsCommand(Guid OrganizationId, int Amount, string CorrelationId, string Reference) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

/// <summary>Consume credits from an active hold for a single unit of work.</summary>
public record ConsumeCreditHoldCommand(Guid OrganizationId, Guid HoldId, int Amount, string Reference) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

/// <summary>Release remaining held credits back to the wallet. Returns the amount released.</summary>
public record ReleaseCreditHoldCommand(Guid OrganizationId, Guid HoldId, string Reference) : ICommand<int>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
