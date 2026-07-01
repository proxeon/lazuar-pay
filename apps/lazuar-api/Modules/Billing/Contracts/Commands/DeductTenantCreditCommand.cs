// apps/lazuar-api/Modules/Billing/Contracts/Commands/DeductTenantCreditCommand.cs
using System;
using BuildingBlocks.Application;

namespace Modules.Billing.Contracts.Commands;

public record DeductTenantCreditCommand(Guid OrganizationId, int Amount, string Reference, string? IdempotencyKey = null) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

/// <summary>
/// Claw back credits after a chargeback/dispute. Recovers up to the available balance without
/// throwing (the tenant may have already spent the credits).
/// </summary>
public record ClawbackCreditsCommand(Guid OrganizationId, int Amount, string Reference) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
