using System;
using BuildingBlocks.Application;

namespace Modules.Payments.Contracts.Events;

/// <summary>
/// Dispatched by domain modules to instruct the Payments module to asynchronously charge a vaulted payment method.
/// </summary>
public record ExecuteOffSessionChargeIntegrationEvent(
    Guid TenantId,
    Guid SubscriptionId,
    decimal Amount,
    string Currency,
    string GatewayCustomerId,
    string GatewayTokenId) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
