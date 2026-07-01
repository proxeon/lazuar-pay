using System;
using System.Collections.Generic;
using BuildingBlocks.Application;

namespace Modules.Payments.Contracts.Events;

/// <summary>
/// Raised when a gateway reports a chargeback/dispute on a prior payment. Billing consumes
/// this to claw back any credits that were granted for a disputed utility-credit top-up.
/// </summary>
public record GatewayDisputeCreatedIntegrationEvent(
    Guid OrganizationId,
    string GatewayTransactionId,
    decimal AmountDisputed,
    string Currency,
    IReadOnlyDictionary<string, string> Metadata) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
