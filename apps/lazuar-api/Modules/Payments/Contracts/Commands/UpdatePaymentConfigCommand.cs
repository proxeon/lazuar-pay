using System;
using BuildingBlocks.Application;

namespace Modules.Payments.Application.Commands;

public record UpdatePaymentConfigCommand(
    Guid OrganizationId,
    string GatewayType,
    string? ApiKey,
    string? MerchantId,
    string? WebhookSecret,
    string? SecretKey,
    bool IsActive) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
