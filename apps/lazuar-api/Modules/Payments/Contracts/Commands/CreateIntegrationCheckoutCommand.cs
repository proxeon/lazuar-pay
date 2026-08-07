using System;
using System.Collections.Generic;
using BuildingBlocks.Application;

namespace Modules.Payments.Contracts.Commands;

/// <summary>
/// M2M ad-hoc checkout: amount + metadata + return URLs (no product slug / CRM).
/// </summary>
public record CreateIntegrationCheckoutCommand(
    Guid OrganizationId,
    decimal Amount,
    string Currency,
    string Description,
    string CustomerEmail,
    string SuccessUrl,
    string CancelUrl,
    string? CustomerName = null,
    string? GatewayName = null,
    bool SetupFutureUsage = false,
    string? IdempotencyKey = null,
    Dictionary<string, string>? Metadata = null) : ICommand<IntegrationCheckoutResult>
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
}

public record IntegrationCheckoutResult(
    Guid CheckoutId,
    string? CheckoutUrl,
    string Gateway,
    string Status,
    decimal Amount,
    string Currency,
    string? ProviderSessionId,
    string? GatewayTransactionId,
    DateTime ExpiresAt,
    Dictionary<string, string> Metadata);
