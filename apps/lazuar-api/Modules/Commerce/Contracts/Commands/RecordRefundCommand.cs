using System;
using BuildingBlocks.Application;

namespace Modules.Commerce.Contracts.Commands;

/// <summary>
/// Ops-initiated refund. API rails publish <c>GatewayRefundRequested</c>; mark-refunded
/// rails apply immediately and publish <c>GatewayRefundCompleted</c> (no adapter).
/// Returns <c>refund_requested</c> or <c>refunded</c>.
/// </summary>
public record RecordRefundCommand(
    Guid OrganizationId,
    Guid TransactionLogId,
    decimal? Amount = null,
    string? GatewayName = null,
    Guid? SubscriptionId = null,
    decimal TaxAmount = 0m,
    bool MarkRefunded = false,
    string? Reason = null) : ICommand<string>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
