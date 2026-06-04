using System;
using BuildingBlocks.Application;

namespace Modules.Payments.Application.Queries;

public record GetPaymentConfigQuery(Guid OrganizationId) : IQuery<PaymentConfigDto?>;

public record PaymentConfigDto(
    string GatewayType,
    string? ApiKey,
    string? MerchantId,
    string? WebhookSecret,
    string? SecretKey,
    bool IsActive);
