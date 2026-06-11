using System;
using BuildingBlocks.Application;

namespace Modules.Billing.Contracts.Events;

public record CommissionAccruedIntegrationEvent(
    Guid OrganizationId,
    Guid AffiliateId,
    string CommissionId,
    decimal Amount,
    string Currency) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
