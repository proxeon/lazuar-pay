using System;
using BuildingBlocks.Application;

namespace Modules.Commerce.Contracts.Commands;

/// <summary>
/// Ops-initiated refund: publishes <c>GatewayRefundRequestedIntegrationEvent</c> for the Payments module.
/// Prefer loading a commerce transaction log id so amount/currency/gateway tx id are taken from the ledger.
/// </summary>
public record RecordRefundCommand(
    Guid OrganizationId,
    Guid TransactionLogId,
    decimal? Amount = null,
    string? GatewayName = null,
    Guid? SubscriptionId = null,
    decimal TaxAmount = 0m) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
