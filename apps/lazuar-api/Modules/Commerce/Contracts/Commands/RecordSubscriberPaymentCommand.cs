using System;
using BuildingBlocks.Application;

namespace Modules.Commerce.Contracts.Commands;

/// <summary>
/// Ops offline payment for an existing subscription: advance period, clear dunning,
/// write MANUAL transaction log, and publish ledger/fulfillment events as appropriate.
/// </summary>
public record RecordSubscriberPaymentCommand(
    Guid OrganizationId,
    Guid SubscriptionId,
    decimal Amount,
    string PaymentMethod,
    string? ReferenceNumber,
    DateTime? NextBillingDate = null) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
