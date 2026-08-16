using System;
using System.Text.Json.Serialization;
using BuildingBlocks.Application;

namespace Modules.Payments.Contracts.Commands;

public record UpdatePaymentConfigCommand(
    Guid OrganizationId,
    string GatewayType,
    string? ApiKey,
    [property: JsonPropertyName("collection_id")] string? MerchantId,
    string? WebhookSecret,
    string? SecretKey,
    bool? IsActive = null,
    string? Environment = null) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
