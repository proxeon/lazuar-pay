using System;
using System.Collections.Generic;
using BuildingBlocks.Application;
using Modules.Payments.Contracts.Results;

namespace Modules.Payments.Contracts.Queries;

/// <summary>
/// Same cashier path as <see cref="GenerateCheckoutSessionQuery"/> but returns
/// provider session id and resolved gateway name (required for M2M correlation).
/// </summary>
public record GenerateCheckoutSessionDetailedQuery(
    Guid TenantId,
    decimal Amount,
    string Currency,
    string ProductName,
    string CustomerEmail,
    string SuccessUrl,
    string CancelUrl,
    Dictionary<string, string> Metadata,
    bool SetupFutureUsage = false,
    int Quantity = 1,
    string? GatewayName = null,
    /// <summary>
    /// When true (M2M default), no active BYOK config → fail with PAYMENTS_NOT_CONFIGURED
    /// instead of falling back to an unconfigured BILLPLZ name.
    /// </summary>
    bool RequireActiveGateway = true) : IQuery<GenerateCheckoutSessionResult>;
