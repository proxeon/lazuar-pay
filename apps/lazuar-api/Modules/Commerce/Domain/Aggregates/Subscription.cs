using System;
using System.Collections.Generic;
using BuildingBlocks.Domain;
using Modules.Commerce.Domain.Entities;

namespace Modules.Commerce.Domain.Aggregates;

public class Subscription : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public Guid ClientProfileId { get; private set; }
    public Guid ProductId { get; private set; }
    public string Status { get; private set; }
    public DateTime? CurrentPeriodEnd { get; private set; }
    public DateTime? NextBillingDate { get; private set; }
    public string? VaultedCustomerId { get; private set; }
    public string? VaultedTokenId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<ReminderDispatchLog> _reminderLogs = new();
    public IReadOnlyCollection<ReminderDispatchLog> ReminderLogs => _reminderLogs.AsReadOnly();

#pragma warning disable CS8618
    private Subscription() { }
#pragma warning restore CS8618

    public Subscription(Guid organizationId, Guid clientProfileId, Guid productId)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        ClientProfileId = clientProfileId;
        ProductId = productId;
        Status = "PENDING";
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate(DateTime currentPeriodEnd, DateTime nextBillingDate)
    {
        Status = "ACTIVE";
        CurrentPeriodEnd = currentPeriodEnd;
        NextBillingDate = nextBillingDate;
        UpdatedAt = DateTime.UtcNow;
    }

    public void StoreVaultedToken(string customerId, string tokenId)
    {
        VaultedCustomerId = customerId;
        VaultedTokenId = tokenId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsPastDue()
    {
        Status = "PAST_DUE";
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        Status = "CANCELED";
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordReminderDispatched(Guid scheduleId, DateTime targetBillingDate)
    {
        _reminderLogs.Add(new ReminderDispatchLog(Id, scheduleId, targetBillingDate));
    }
}
