using System;
using BuildingBlocks.Application;

namespace Modules.Billing.Contracts.Commands;

public record GenerateAndStorePlatformSaasInvoiceCommand(
    Guid PayingOrganizationId,
    Guid LedgerEntryId) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
