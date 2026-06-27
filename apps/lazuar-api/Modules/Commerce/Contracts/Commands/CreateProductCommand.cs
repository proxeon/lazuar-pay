using System;
using System.Collections.Generic;
using BuildingBlocks.Application;

namespace Modules.Commerce.Contracts.Commands;

public record CreateProductCommand(
    Guid OrganizationId,
    string Name,
    string Slug,
    decimal Price,
    string Currency,
    string Interval,
    bool RequiresAddress,
    bool RequiresTaxId,
    bool RequiresPhone,
    List<string> FulfillmentTargets) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
