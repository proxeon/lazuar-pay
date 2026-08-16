using System;
using System.Collections.Generic;
using BuildingBlocks.Domain;
using Modules.Commerce.Domain.Entities;
using Modules.Commerce.Domain.ValueObjects;

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
    public bool CancelAtPeriodEnd { get; private set; }
    
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

    /// <summary>
    /// Frozen campaign definition for the current PAST_DUE run. Null when not in dunning.
    /// Written once at assign; engine must not re-read live steps/grace/final.
    /// </summary>
    public string? DunningCampaignSnapshotJson { get; private set; }

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
        CancelAtPeriodEnd = false;
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
        ClearScheduledCancel();
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
        ClearScheduledCancel();
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

    public void ScheduleCancelAtPeriodEnd()
    {
        if (Status != "ACTIVE")
            throw new InvalidOperationException($"Cannot schedule cancel from status '{Status}'.");
        if (NextBillingDate is null || NextBillingDate.Value <= DateTime.UtcNow)
            throw new InvalidOperationException("No remaining paid period.");
        CancelAtPeriodEnd = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ClearScheduledCancel()
    {
        CancelAtPeriodEnd = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        Status = "CANCELED";
        CancelAtPeriodEnd = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordReminderDispatched(Guid scheduleId, DateTime targetBillingDate, int dayOffset)
    {
        _reminderLogs.Add(new ReminderDispatchLog(Id, scheduleId, targetBillingDate, dayOffset));
        MarkDunningStepCompleted(dayOffset);
    }

    /// <summary>
    /// Pins a campaign id without a plan. Engine lazy-backfills JSON from the live campaign.
    /// Production assign sites must use the snapshot overload.
    /// </summary>
    public void AssignDunningCampaign(Guid campaignId)
    {
        AssignDunningCampaignCore(campaignId, snapshotJson: null);
    }

    public void AssignDunningCampaign(Guid campaignId, DunningCampaignSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.CampaignId != campaignId)
        {
            throw new ArgumentException("Snapshot campaign_id must match the assigned campaign.", nameof(snapshot));
        }

        AssignDunningCampaignCore(campaignId, snapshot.Serialize());
    }

    /// <summary>Writes snapshot JSON without resetting step progress (pre-migration lazy backfill).</summary>
    public void CaptureDunningCampaignSnapshot(DunningCampaignSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (CurrentDunningCampaignId is null)
        {
            throw new InvalidOperationException("Cannot capture a dunning snapshot without an assigned campaign.");
        }

        if (snapshot.CampaignId != CurrentDunningCampaignId)
        {
            throw new ArgumentException("Snapshot campaign_id must match the assigned campaign.", nameof(snapshot));
        }

        DunningCampaignSnapshotJson = snapshot.Serialize();
        UpdatedAt = DateTime.UtcNow;
    }

    public DunningCampaignSnapshot? TryGetDunningCampaignSnapshot() =>
        DunningCampaignSnapshot.TryParse(DunningCampaignSnapshotJson);

    private void AssignDunningCampaignCore(Guid campaignId, string? snapshotJson)
    {
        CurrentDunningCampaignId = campaignId;
        DunningCampaignSnapshotJson = snapshotJson;
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
        DunningCampaignSnapshotJson = null;
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
