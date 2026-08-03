using System;
using System.Collections.Generic;
using BuildingBlocks.Application;

namespace Modules.Payments.Contracts.Queries;

public record GenerateCheckoutSessionQuery(
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
    /// <summary>
    /// Preferred gateway (e.g. product.GatewayName). When null/empty, Payments resolves
    /// the first configured gateway for the tenant, then BILLPLZ as last resort.
    /// </summary>
    string? GatewayName = null) : IQuery<string>;
