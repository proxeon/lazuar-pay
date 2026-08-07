using System;
using BuildingBlocks.Application;
using Modules.Payments.Contracts.Commands;

namespace Modules.Payments.Contracts.Queries;

public record GetIntegrationCheckoutQuery(
    Guid OrganizationId,
    Guid CheckoutId) : IQuery<IntegrationCheckoutResult?>;
