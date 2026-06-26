// apps/lazuar-api/Modules/Payments/Contracts/Events/ApiCreditPurchasedIntegrationEvent.cs
using System;
using BuildingBlocks.Application;

namespace Modules.Payments.Contracts.Events;

public record ApiCreditPurchasedIntegrationEvent(
    Guid OrganizationId,
    int CreditAmount,
    decimal AmountPaid,
    string Currency,
    string GatewayTransactionId) : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
