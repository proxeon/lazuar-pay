using System;
using System.Collections.Generic;
using BuildingBlocks.Application;

namespace Modules.Commerce.Contracts.Commands;

public record CreateProductCommand(
    Guid OrganizationId,
    string Name,
    string Slug,
    decimal Price,
    string PricingModel,
    decimal MinimumPrice,
    string Currency,
    string Interval,
    string GatewayName,
    bool RequiresAddress,
    bool RequiresTaxId,
    bool RequiresPhone,
    List<string> FulfillmentTargets,
    string? SstTaxType = null,
    decimal SstRatePercent = 0) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
