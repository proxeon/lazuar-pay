using System;
using System.Collections.Generic;
using BuildingBlocks.Application;

namespace Modules.Payments.Contracts.Events;

public record GatewayPaymentCompletedIntegrationEvent(
    Guid OrganizationId,
    string GatewayTransactionId,
    decimal AmountPaid,
    string Currency,
    decimal GatewayFee,
    decimal TaxAmount,
    decimal NetAmount,
    decimal FxRate,
    string BaseCurrency,
    List<LineItemDto> LineItems,
    Dictionary<string, string> Metadata,
    string? GatewayCustomerId = null,
    string? GatewayTokenId = null) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

public record LineItemDto(
    string Sku,
    string Description,
    decimal Amount,
    string RevenueType
);
