using System;
using System.Text.Json.Serialization;
using BuildingBlocks.Application;

namespace Modules.Payments.Application.Commands;

public record UpdatePaymentConfigCommand(
    Guid OrganizationId,
    string GatewayType,
    string? ApiKey,
    // Explicitly map collection_id to MerchantId
    [property: JsonPropertyName("collection_id")] string? MerchantId, 
    string? WebhookSecret,
    string? SecretKey,
    bool IsActive) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
