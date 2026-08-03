using System;
using BuildingBlocks.Application;

namespace Modules.Payments.Contracts.Events;

public record ExecuteOffSessionChargeIntegrationEvent(
    Guid TenantId,
    Guid SubscriptionId,
    decimal Amount,
    string Currency,
    string GatewayCustomerId,
    string GatewayTokenId,
    Guid? DunningCampaignId = null,
    string GatewayName = "STRIPE",
    Guid? ChargeAttemptId = null) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
