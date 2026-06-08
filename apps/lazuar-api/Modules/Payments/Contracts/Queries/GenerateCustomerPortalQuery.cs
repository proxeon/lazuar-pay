using System;
using BuildingBlocks.Application;

namespace Modules.Payments.Contracts.Queries;

public record GenerateCustomerPortalQuery(
    Guid TenantId,
    string CustomerEmail,
    string ReturnUrl) : IQuery<string>;
