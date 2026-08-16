using System;
using BuildingBlocks.Domain;

namespace Modules.Billing.Domain.Aggregates;

public static class WorkspaceSaasStatuses
{
    public const string Unpaid = "UNPAID";
    public const string Active = "ACTIVE";
    public const string PastDue = "PAST_DUE";
    public const string Canceled = "CANCELED";
}

/// <summary>
/// One Hub software subscription per workspace (plane S). Not a Commerce product
/// and not a module entitlement.
/// </summary>
public class WorkspaceSaasSubscription : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public string PlanCode { get; private set; }
    public string Status { get; private set; }
    public DateTime? CurrentPeriodStart { get; private set; }
    public DateTime? CurrentPeriodEnd { get; private set; }
    public DateTime? NextInvoiceAt { get; private set; }
    public string? LastGatewayTransactionId { get; private set; }
    public DateTime UpdatedAt { get; private set; }

#pragma warning disable CS8618
    private WorkspaceSaasSubscription() { }
#pragma warning restore CS8618

    public WorkspaceSaasSubscription(Guid organizationId, string planCode)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        PlanCode = planCode;
        Status = WorkspaceSaasStatuses.Unpaid;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkUnpaid()
    {
        Status = WorkspaceSaasStatuses.Unpaid;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Starts or extends the paid period from <c>max(now, CurrentPeriodEnd)</c>
    /// so an early renewal does not discard remaining days.
    /// </summary>
    public void ActivateFromPayment(DateTime utcNow, string interval, string? gatewayTransactionId = null)
    {
        var start = CurrentPeriodEnd.HasValue && CurrentPeriodEnd.Value > utcNow
            ? CurrentPeriodEnd.Value
            : utcNow;
        var end = SaasPlanInterval.AddPeriod(start, interval);

        Status = WorkspaceSaasStatuses.Active;
        CurrentPeriodStart = start;
        CurrentPeriodEnd = end;
        NextInvoiceAt = end;
        if (!string.IsNullOrWhiteSpace(gatewayTransactionId))
            LastGatewayTransactionId = gatewayTransactionId;
        UpdatedAt = utcNow;
    }

    public void MarkPastDue()
    {
        Status = WorkspaceSaasStatuses.PastDue;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        Status = WorkspaceSaasStatuses.Canceled;
        UpdatedAt = DateTime.UtcNow;
    }
}
