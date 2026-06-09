// apps/lazuar-api/Modules/Payments/Contracts/Commands/UpdatePaymentConfigCommand.cs
using System;
using System.Text.Json.Serialization;
using BuildingBlocks.Application;

namespace Modules.Payments.Application.Commands;

[AgentTool("Configure or toggle the active payment gateway (Stripe or Billplz).", "high", "SUPER_ADMIN", "ADMIN")]
public record UpdatePaymentConfigCommand(
    Guid OrganizationId,
    string GatewayType,
    string? ApiKey,
    [property: JsonPropertyName("collection_id")] string? MerchantId, 
    string? WebhookSecret,
    string? SecretKey,
    bool IsActive) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
