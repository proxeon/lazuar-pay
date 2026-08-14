using System;
using System.Collections.Generic;
using BuildingBlocks.Application;

namespace Modules.Payments.Contracts.Queries;

public record GetPaymentsMeQuery(
    Guid OrganizationId,
    Guid CredentialId,
    bool IsTestMode,
    IReadOnlyList<string> Scopes,
    string? KeyName) : IQuery<PaymentsMeResult>;

public record PaymentsMeResult(
    Guid WorkspaceId,
    Guid OrganizationId,
    bool IsTestMode,
    Guid KeyId,
    string? KeyName,
    IReadOnlyList<string> Scopes,
    bool HasActiveGateway,
    IReadOnlyList<string> GatewayNames);
