using System;
using System.Collections.Generic;
using BuildingBlocks.Domain;
using Modules.Community.Domain.Entities;
using Modules.Community.Domain.Events;
using Modules.Community.Domain.Rules;

namespace Modules.Community.Domain.Aggregates;

public class CommunitySubscription : Entity, IAggregateRoot, IMustHaveTenant
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; set; }
    public Guid ClientProfileId { get; private set; }
    public Guid PlanId { get; private set; }
    public Guid? PendingPlanId { get; private set; }
    public string Status { get; private set; }
    public DateTime? CurrentPeriodEnd { get; private set; }
    public DateTime? NextRenewalDate { get; private set; }
    public string Source { get; private set; }
    public string? PreferredChannel { get; private set; }
    public bool IsReminderOnly { get; private set; }
    public string? AdminNotes { get; private set; }
    public DateTime? RemindersPausedUntil { get; private set; }
    public string? PaymentGatewaySessionId { get; private set; }
    public string? GatewaySubscriptionId { get; private set; }
    public Guid? PendingCouponId { get; private set; }
    public string? VaultedCustomerId { get; private set; }
    public string? VaultedTokenId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<PaymentRecord> _paymentRecords = new();
    public IReadOnlyCollection<PaymentRecord> PaymentRecords => _paymentRecords.AsReadOnly();

    private readonly List<ReminderDispatchLog> _reminderLogs = new();
    public IReadOnlyCollection<ReminderDispatchLog> ReminderLogs => _reminderLogs.AsReadOnly();

#pragma warning disable CS8618
    private CommunitySubscription() { }
#pragma warning restore CS8618

    public CommunitySubscription(
        Guid organizationId, Guid clientProfileId, Guid planId,
        string source, bool isReminderOnly, string? preferredChannel, string? adminNotes = null)
    {
        Id = Guid.CreateVersion7();
        OrganizationId = organizationId;
        ClientProfileId = clientProfileId;
        PlanId = planId;
        Status = "PENDING";
        Source = source;
        IsReminderOnly = isReminderOnly;
        PreferredChannel = preferredChannel;
        AdminNotes = adminNotes;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void StoreVaultedToken(string customerId, string tokenId)
    {
        VaultedCustomerId = customerId;
        VaultedTokenId = tokenId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ClearVaultedToken()
    {
        VaultedCustomerId = null;
        VaultedTokenId = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void InitiateCheckout()
    {
        AddDomainEvent(new CheckoutInitiatedDomainEvent(Id, OrganizationId, ClientProfileId));
    }

    public void SetPaymentGatewaySessionId(string sessionId)
    {
        PaymentGatewaySessionId = sessionId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SchedulePlanChange(Guid newPlanId)
    {
        PendingPlanId = newPlanId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPendingCoupon(Guid? couponId)
    {
        PendingCouponId = couponId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ClearPendingCoupon()
    {
        PendingCouponId = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate(
        DateTime periodStart, DateTime periodEnd, decimal amount,
        string currency, string paymentMethod, string? externalReference, string recordedBy, string? receiptUrl = null, bool isSilent = false)
    {
        CheckRule(new InvalidSubscriptionStateTransitionRule(Status, "ACTIVE", IsReminderOnly));
        bool isFirstPayment = Status == "PENDING";

        if (PendingPlanId.HasValue)
        {
            PlanId = PendingPlanId.Value;
            PendingPlanId = null;
        }

        Status = "ACTIVE";
        CurrentPeriodEnd = periodEnd;
        NextRenewalDate = periodEnd;
        UpdatedAt = DateTime.UtcNow;

        var payment = new PaymentRecord(
            Id, amount, currency, paymentMethod, externalReference,
            recordedBy, periodStart, periodEnd,
            isFirstPayment ? "Initial subscription payment" : "Renewal payment",
            receiptUrl);

        _paymentRecords.Add(payment);
        AddDomainEvent(new SubscriptionActivatedDomainEvent(Id, OrganizationId, ClientProfileId, isFirstPayment, isSilent));
    }

    public void Reactivate(string recordedBy)
    {
        CheckRule(new InvalidSubscriptionStateTransitionRule(Status, "ACTIVE", IsReminderOnly));
        Status = "ACTIVE";

        var now = DateTime.UtcNow;
        var periodEnd = now.AddDays(30);
        CurrentPeriodEnd = periodEnd;
        NextRenewalDate = periodEnd;
        UpdatedAt = now;

        var payment = new PaymentRecord(
            Id, 0m, "MYR", "SYSTEM_REACTIVATION", null,
            recordedBy, now, periodEnd, "Manual account reactivation");

        _paymentRecords.Add(payment);
        AddDomainEvent(new SubscriptionActivatedDomainEvent(Id, OrganizationId, ClientProfileId, false, false));
    }

    public void ReplayActivationEvent()
    {
        AddDomainEvent(new SubscriptionActivatedDomainEvent(Id, OrganizationId, ClientProfileId, true, false));
    }

    public void MarkAsPastDue()
    {
        CheckRule(new InvalidSubscriptionStateTransitionRule(Status, "PAST_DUE", IsReminderOnly));
        Status = "PAST_DUE";
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        CheckRule(new InvalidSubscriptionStateTransitionRule(Status, "CANCELLED", IsReminderOnly));
        Status = "CANCELLED";
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new SubscriptionCancelledDomainEvent(Id, OrganizationId, ClientProfileId));
    }

    public void Expire()
    {
        CheckRule(new InvalidSubscriptionStateTransitionRule(Status, "EXPIRED", IsReminderOnly));
        Status = "EXPIRED";
        UpdatedAt = DateTime.UtcNow;
    }

    public void Ban()
    {
        CheckRule(new InvalidSubscriptionStateTransitionRule(Status, "BANNED", IsReminderOnly));
        Status = "BANNED";
        CurrentPeriodEnd = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new SubscriptionBannedDomainEvent(Id, OrganizationId, ClientProfileId));
    }

    public void RequestRefund(Guid paymentRecordId)
    {
        AddDomainEvent(new RefundRequestedDomainEvent(Id, paymentRecordId));
    }

    public void RecordRefund(decimal refundAmount, string currency, string originalReference)
    {
        var negativeAmount = refundAmount > 0 ? -refundAmount : refundAmount;
        var refundRecord = new PaymentRecord(
            Id, negativeAmount, currency, "SYSTEM_REFUND", originalReference,
            "SYSTEM", DateTime.UtcNow, DateTime.UtcNow, "Refund processed");

        _paymentRecords.Add(refundRecord);
        UpdatedAt = DateTime.UtcNow;
    }

    public void ExtendGracePeriod(int days)
    {
        var baseDate = NextRenewalDate ?? CurrentPeriodEnd ?? DateTime.UtcNow;
        var newDate = baseDate.AddDays(days);

        if (Status != "ACTIVE")
        {
            CheckRule(new InvalidSubscriptionStateTransitionRule(Status, "ACTIVE", IsReminderOnly));
            Status = "ACTIVE";
        }

        CurrentPeriodEnd = newDate;
        NextRenewalDate = newDate;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new SubscriptionGracePeriodExtendedDomainEvent(Id, OrganizationId, ClientProfileId, days, newDate));
    }

    public void PauseReminders(DateTime? pauseUntil)
    {
        RemindersPausedUntil = pauseUntil;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new SubscriptionRemindersPausedDomainEvent(Id, OrganizationId, ClientProfileId, pauseUntil));
    }

    public void UpdateProfile(bool isReminderOnly, string? preferredChannel, string? adminNotes, DateTime? nextRenewalDate)
    {
        IsReminderOnly = isReminderOnly;
        PreferredChannel = preferredChannel;
        AdminNotes = adminNotes;
        if (nextRenewalDate.HasValue) NextRenewalDate = nextRenewalDate.Value;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new SubscriptionProfileUpdatedDomainEvent(Id, OrganizationId, ClientProfileId, isReminderOnly, preferredChannel, nextRenewalDate));
    }

    public void RequestMagicLink(string magicLinkUrl)
    {
        AddDomainEvent(new MagicLinkRequestedDomainEvent(Id, OrganizationId, ClientProfileId, magicLinkUrl));
    }

    public void SendOneOffReminder(Guid? templateId, string? customMessage, string channel)
    {
        if (!templateId.HasValue && string.IsNullOrWhiteSpace(customMessage))
        {
            throw new InvalidOperationException("Either a template ID or a custom message must be provided to send a reminder.");
        }
        if (string.IsNullOrWhiteSpace(channel))
        {
            throw new ArgumentException("Channel cannot be empty.", nameof(channel));
        }

        AddDomainEvent(new OneOffReminderRequestedDomainEvent(
            Id,
            OrganizationId,
            ClientProfileId,
            templateId,
            customMessage,
            channel.ToUpperInvariant()));
    }

    public void ScheduleOneOffReminder(Guid? templateId, string? customMessage, string channel, DateTime scheduledAt)
    {
        if (!templateId.HasValue && string.IsNullOrWhiteSpace(customMessage))
        {
            throw new InvalidOperationException("Either a template ID or a custom message must be provided to schedule a reminder.");
        }
        if (string.IsNullOrWhiteSpace(channel))
        {
            throw new ArgumentException("Channel cannot be empty.", nameof(channel));
        }

        AddDomainEvent(new OneOffReminderRequestedDomainEvent(
            Id,
            OrganizationId,
            ClientProfileId,
            templateId,
            customMessage,
            channel.ToUpperInvariant(),
            scheduledAt));
    }

    public void RecordReminderDispatched(Guid scheduleId, DateTime targetRenewalDate)
    {
        _reminderLogs.Add(new ReminderDispatchLog(Id, scheduleId, targetRenewalDate));
    }
}
