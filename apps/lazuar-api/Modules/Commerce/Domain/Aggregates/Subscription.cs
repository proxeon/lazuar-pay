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
    public bool IsReminderOnly { get; private set; }
    
    public Guid? CurrentDunningCampaignId { get; private set; }
    /// <summary>Legacy progress field; kept in sync with <see cref="LastCompletedDayOffset"/> for compatibility.</summary>
    public int CurrentDunningStepIndex { get; private set; }
    /// <summary>Highest DayOffset successfully dispatched for the current dunning run; null when not in dunning.</summary>
    public int? LastCompletedDayOffset { get; private set; }
    public DateTime? DunningPausedUntil { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? SuspendedAt { get; private set; }

    /// <summary>
    /// JSON object persisted from checkout (aura_org_id, type, billing_interval).
    /// Survives session expiry so renewals can emit metadata on subscription.* webhooks.
    /// </summary>
    public string? MetadataJson { get; private set; }

    /// <summary>Hosted checkout URL minted for the current non-vaulted renewal cycle.</summary>
    public string? CurrentRenewalCheckoutUrl { get; private set; }

    /// <summary>Billing date the current renewal checkout was minted for.</summary>
    public DateTime? CurrentRenewalCheckoutForDate { get; private set; }

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
        IsReminderOnly = false;
        CurrentDunningStepIndex = 0;
        LastCompletedDayOffset = null;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate(DateTime currentPeriodEnd, DateTime? nextBillingDate, bool isReminderOnly = false)
    {
        // Prevent cycle advancement if the subscription was in arrears and is just updating config
        if (Status == "PAST_DUE" || Status == "SUSPENDED")
        {
            NextBillingDate = NextBillingDate; 
            CurrentPeriodEnd = CurrentPeriodEnd;
        }
        else
        {
            CurrentPeriodEnd = currentPeriodEnd;
            NextBillingDate = nextBillingDate;
            ClearCurrentRenewalCheckout();
        }

        Status = "ACTIVE";
        IsReminderOnly = isReminderOnly;
        SuspendedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void StoreVaultedToken(string customerId, string tokenId)
    {
        VaultedCustomerId = customerId;
        VaultedTokenId = tokenId;
        IsReminderOnly = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsPastDue()
    {
        Status = "PAST_DUE";
        UpdatedAt = DateTime.UtcNow;
    }

    public void Suspend()
    {
        Status = "SUSPENDED";
        SuspendedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Resume(DateTime newNextBillingDate)
    {
        Status = "ACTIVE";
        SuspendedAt = null;
        NextBillingDate = newNextBillingDate;
        ClearDunning();
        ClearCurrentRenewalCheckout();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Recover from successful payment while in PAST_DUE (or similar arrears).
    /// Unlike <see cref="Activate"/>, this always advances period dates and clears dunning.
    /// </summary>
    public void RecoverFromPayment(DateTime periodEnd, DateTime nextBilling)
    {
        Status = "ACTIVE";
        CurrentPeriodEnd = periodEnd;
        NextBillingDate = nextBilling;
        SuspendedAt = null;
        ClearDunning();
        ClearCurrentRenewalCheckout();
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetCurrentRenewalCheckout(string url, DateTime forDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        CurrentRenewalCheckoutUrl = url.Trim();
        CurrentRenewalCheckoutForDate = DateTime.SpecifyKind(forDate.Date, DateTimeKind.Utc);
        UpdatedAt = DateTime.UtcNow;
    }

    public void ClearCurrentRenewalCheckout()
    {
        CurrentRenewalCheckoutUrl = null;
        CurrentRenewalCheckoutForDate = null;
    }

    public void Cancel()
    {
        Status = "CANCELED";
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordReminderDispatched(Guid scheduleId, DateTime targetBillingDate, int dayOffset)
    {
        _reminderLogs.Add(new ReminderDispatchLog(Id, scheduleId, targetBillingDate, dayOffset));
        MarkDunningStepCompleted(dayOffset);
    }

    public void AssignDunningCampaign(Guid campaignId)
    {
        CurrentDunningCampaignId = campaignId;
        CurrentDunningStepIndex = 0;
        LastCompletedDayOffset = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AdvanceDunningStep()
    {
        CurrentDunningStepIndex++;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records the highest successfully completed dunning DayOffset for ops visibility.
    /// </summary>
    public void MarkDunningStepCompleted(int dayOffset)
    {
        if (LastCompletedDayOffset == null || dayOffset > LastCompletedDayOffset.Value)
        {
            LastCompletedDayOffset = dayOffset;
        }

        CurrentDunningStepIndex = LastCompletedDayOffset ?? 0;
        UpdatedAt = DateTime.UtcNow;
    }

    public void PauseDunning(DateTime until)
    {
        DunningPausedUntil = until;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ResumeDunning()
    {
        DunningPausedUntil = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ClearDunning()
    {
        CurrentDunningCampaignId = null;
        CurrentDunningStepIndex = 0;
        LastCompletedDayOffset = null;
        DunningPausedUntil = null;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>No-op when <paramref name="metadataJson"/> is empty so renewals keep the first-checkout map.</summary>
    public void SetMetadataJson(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return;
        }

        MetadataJson = metadataJson;
        UpdatedAt = DateTime.UtcNow;
    }
}
