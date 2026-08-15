using System;
using BuildingBlocks.Application;

namespace Modules.Billing.Contracts.Commands;

public record GenerateAndStoreDocumentCommand(
    Guid OrganizationId,
    Guid LedgerEntryId,
    string DocumentType,
    string? LhdnQrLink = null,
    string? CorrelationId = null) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
