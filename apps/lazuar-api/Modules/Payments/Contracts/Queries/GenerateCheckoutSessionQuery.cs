using System;
using System.Collections.Generic;
using BuildingBlocks.Application;

namespace Modules.Payments.Contracts.Queries;

/// <summary>
/// Payments hop-2 checkout. <paramref name="Amount"/> is unit major units after per-unit
/// discount (what one item costs). <paramref name="Quantity"/> multiplies inside the adapter.
/// Line total the buyer pays = Amount × Quantity. Callers that already have a line total
/// (custom session sum, M2M, renewal) must pass Quantity = 1.
/// </summary>
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
