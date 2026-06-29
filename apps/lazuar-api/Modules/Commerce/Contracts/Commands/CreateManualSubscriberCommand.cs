using System;
using BuildingBlocks.Application;

namespace Modules.Commerce.Contracts.Commands;

public record CreateManualSubscriberCommand(
    Guid OrganizationId,
    string Name,
    string Email,
    string Phone,
    Guid ProductId,
    string PaymentMethod,
    decimal AmountPaid,
    string? ReferenceNumber,
    bool SendWelcomeEmail,
    DateTime? StartDate,
    DateTime? NextBillingDate
) : ICommand<Guid>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
