// apps/lazuar-api/Modules/Billing/Contracts/Commands/DeductTenantCreditCommand.cs
using System;
using BuildingBlocks.Application;

namespace Modules.Billing.Contracts.Commands;

public record DeductTenantCreditCommand(Guid OrganizationId, int Amount, string Reference) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
