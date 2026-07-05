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
    string GatewayName = "BILLPLZ") : IQuery<string>;
