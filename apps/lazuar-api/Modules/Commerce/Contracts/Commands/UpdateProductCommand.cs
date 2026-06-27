using System;
using System.Collections.Generic;
using BuildingBlocks.Application;

namespace Modules.Commerce.Contracts.Commands;

public record UpdateProductCommand(
    Guid OrganizationId,
    Guid ProductId,
    string Name,
    string Slug,
    decimal Price,
    string Currency,
    string Interval,
    bool IsActive,
    bool RequiresAddress,
    bool RequiresTaxId,
    bool RequiresPhone,
    List<string> FulfillmentTargets) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
