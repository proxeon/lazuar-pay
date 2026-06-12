using System;
using System.Text.Json.Serialization;
using BuildingBlocks.Application;

namespace Modules.Payments.Application.Commands;

// Removed [AgentTool] attribute to revoke AI write access for security reasons.
public record UpdatePaymentConfigCommand(
    Guid OrganizationId,
    string GatewayType,
    string? ApiKey,
    [property: JsonPropertyName("collection_id")] string? MerchantId,
    string? WebhookSecret,
    string? SecretKey,
    bool IsActive,
    decimal EstimatedFeePercentage = 0,
    decimal FixedFee = 0,
    decimal TaxRate = 0) : ICommand
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}
