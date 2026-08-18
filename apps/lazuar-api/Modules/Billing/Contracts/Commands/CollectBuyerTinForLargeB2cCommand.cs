using System;
using BuildingBlocks.Application;

namespace Modules.Billing.Contracts.Commands;

public record CollectBuyerTinForLargeB2cCommand(
    Guid OrganizationId,
    Guid LedgerEntryId,
    string Tin,
    string IdType,
    string IdValue,
    string CompanyName,
    string FullName,
    string Email) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
