using System;
using System.Collections.Generic;
using BuildingBlocks.Application;
using Modules.Commerce.Domain.ValueObjects;

namespace Modules.Commerce.Contracts.Commands;

public record CreateCustomCheckoutCommand(
    Guid OrganizationId,
    string ClientEmail,
    string ClientName,
    List<AdHocLineItem> LineItems,
    DateTime? ExpiresAt,
    bool IsB2bRequired) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
