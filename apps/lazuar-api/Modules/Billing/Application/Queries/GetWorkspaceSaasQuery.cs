using System;
using BuildingBlocks.Application;

namespace Modules.Billing.Application.Queries;

public record GetWorkspaceSaasQuery(Guid OrganizationId) : IQuery<WorkspaceSaasView>;

public record WorkspaceSaasView(
    Guid OrganizationId,
    string Status,
    DateTime? CurrentPeriodStart,
    DateTime? CurrentPeriodEnd,
    DateTime? NextInvoiceAt,
    SaasPlanView Plan);

public record SaasPlanView(
    string Code,
    string Name,
    decimal AmountMyr,
    string Interval,
    string Currency);
