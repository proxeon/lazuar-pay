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

    public int Quantity { get; private set; } = 1;
    public int? PendingQuantity { get; private set; }
    public Guid? PendingProductId { get; private set; }
    public Guid? PriceId { get; private set; }
    public decimal UnitAmount { get; private set; }
    public bool HasUnitSnapshot { get; private set; }
    public string? BillingInterval { get; private set; }
    public DateTime? TrialEndsAt { get; private set; }
    public DateTime? CollectionPausedUntil { get; private set; }
    public bool HasOpenDispute { get; private set; }

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
        Quantity = 1;
        UnitAmount = 0m;
        HasOpenDispute = false;
        CurrentDunningStepIndex = 0;
        LastCompletedDayOffset = null;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate(
        DateTime currentPeriodEnd,
        DateTime? nextBillingDate,
        bool isReminderOnly = false,
        int? quantity = null,
        decimal? unitAmount = null)
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
        TrialEndsAt = null;
        IsReminderOnly = isReminderOnly;
        SuspendedAt = null;
        if (quantity.HasValue || unitAmount.HasValue)
        {
            SetSnapshot(unitAmount ?? UnitAmount, quantity ?? Quantity);
        }

        UpdatedAt = DateTime.UtcNow;
    }

    public void ActivateTrial(DateTime endsAt, bool reminderOnly, int quantity = 1, decimal unitAmount = 0)
    {
        if (endsAt <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("Trial end must be in the future.");
        }

        SetSnapshot(unitAmount, quantity);
        Status = "TRIALING";
        TrialEndsAt = endsAt;
        NextBillingDate = endsAt;
        CurrentPeriodEnd = endsAt;
        IsReminderOnly = reminderOnly;
        SuspendedAt = null;
        ClearCurrentRenewalCheckout();
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetSnapshot(decimal unitAmount, int quantity)
    {
        if (quantity < 1 || quantity > 99)
        {
            throw new InvalidOperationException("Quantity must be between 1 and 99.");
        }

        Quantity = quantity;
        UnitAmount = unitAmount;
        HasUnitSnapshot = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RefreshSnapshot(decimal unitAmount)
    {
        UnitAmount = unitAmount;
        HasUnitSnapshot = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPriceId(Guid? priceId)
    {
        PriceId = priceId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetBillingInterval(string? interval)
    {
        if (string.IsNullOrWhiteSpace(interval))
        {
            return;
        }

        BillingInterval = interval.Trim().ToLowerInvariant();
        UpdatedAt = DateTime.UtcNow;
    }

    public void PauseCollection(DateTime until)
    {
        if (Status != "ACTIVE")
        {
            throw new InvalidOperationException($"Cannot pause collection from status '{Status}'.");
        }

        if (until <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("Collection pause resume date must be in the future.");
        }

        CollectionPausedUntil = until;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ResumeCollection(DateTime? nextBill = null)
    {
        CollectionPausedUntil = null;
        if (nextBill.HasValue && (NextBillingDate == null || NextBillingDate < nextBill.Value))
        {
            NextBillingDate = nextBill.Value;
        }

        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsCollectionPaused(DateTime utcNow) =>
        CollectionPausedUntil.HasValue && CollectionPausedUntil.Value > utcNow;

    /// <summary>
    /// Clock ended the holiday. Same skip-the-invoice rule as manual resume.
    /// </summary>
    public bool TryCompleteExpiredCollectionPause(DateTime utcNow, DateTime nextBill)
    {
        if (!CollectionPausedUntil.HasValue || CollectionPausedUntil.Value > utcNow)
        {
            return false;
        }

        ResumeCollection(nextBill);
        return true;
    }

    public void MarkHasOpenDispute()
    {
        HasOpenDispute = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ClearHasOpenDispute()
    {
        if (!HasOpenDispute)
        {
            return;
        }

        HasOpenDispute = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SchedulePlanChange(Guid productId)
    {
        if (productId == Guid.Empty)
        {
            throw new InvalidOperationException("product_id is required.");
        }

        if (productId == ProductId)
        {
            ClearPendingPlanChange();
            return;
        }

        PendingProductId = productId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ClearPendingPlanChange()
    {
        PendingProductId = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool ApplyPendingPlanChange()
    {
        if (PendingProductId is not Guid pending || pending == Guid.Empty)
        {
            return false;
        }

        ProductId = pending;
        PendingProductId = null;
        UpdatedAt = DateTime.UtcNow;
        return true;
    }

    public void ScheduleQuantity(int qty)
    {
        if (qty < 1 || qty > 99)
        {
            throw new InvalidOperationException("Quantity must be between 1 and 99.");
        }

        if (qty == Quantity)
        {
            PendingQuantity = null;
            UpdatedAt = DateTime.UtcNow;
            return;
        }

        PendingQuantity = qty;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool ApplyPendingQuantity()
    {
        if (PendingQuantity is not int qty)
        {
            return false;
        }

        if (qty < 1 || qty > 99)
        {
            throw new InvalidOperationException("Quantity must be between 1 and 99.");
        }

        Quantity = qty;
        PendingQuantity = null;
        UpdatedAt = DateTime.UtcNow;
        return true;
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
        TrialEndsAt = null;
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
        TrialEndsAt = null;
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
        if (Status is not ("ACTIVE" or "TRIALING"))
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
        TrialEndsAt = null;
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
