using System;
using System.Collections.Generic;
using BuildingBlocks.Application;

namespace Modules.Payments.Contracts.Events;

/// <summary>Gateway closed a chargeback (won / lost / warning_closed).</summary>
public record GatewayDisputeClosedIntegrationEvent(
    Guid OrganizationId,
    string GatewayTransactionId,
    string Outcome,
    IReadOnlyDictionary<string, string>? Metadata = null) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
