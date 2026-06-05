using System;
using System.Collections.Generic;
using BuildingBlocks.Application;

namespace Modules.Payments.Contracts.Events;

public record GatewayPaymentFailedIntegrationEvent(
    Guid OrganizationId,
    string GatewayTransactionId,
    Dictionary<string, string> Metadata) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
