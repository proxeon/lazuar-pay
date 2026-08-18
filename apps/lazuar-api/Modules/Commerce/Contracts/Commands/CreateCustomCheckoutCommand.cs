using System;
using System.Collections.Generic;
using BuildingBlocks.Application;

namespace Modules.Commerce.Contracts.Commands;

public record CustomLineItemData(string Description, int Quantity, decimal UnitPrice);

public record CreateCustomCheckoutCommand(
    Guid OrganizationId,
    string ClientEmail,
    string ClientName,
    List<CustomLineItemData> LineItems,
    DateTime? ExpiresAt,
    bool IsB2bRequired,
    string? GatewayName = null,
    DateTime? DueAt = null,
    string? Terms = null,
    string? Currency = null) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
