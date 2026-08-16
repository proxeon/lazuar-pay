using System;
using BuildingBlocks.Domain;

namespace Modules.Commerce.Domain.Entities;

public class ChargeAttemptLog : Entity
{
    public const string StatusPending = "PENDING";
    public const string StatusSucceeded = "SUCCEEDED";
    public const string StatusFailed = "FAILED";
    public const string StatusSkipped = "SKIPPED";

    public const string SourceBilling = "BILLING";
    public const string SourceDunning = "DUNNING";

    public Guid Id { get; private set; }
    public Guid SubscriptionId { get; private set; }
    public DateTime TargetBillingDate { get; private set; }
    public DateTime AttemptedAt { get; private set; }
    public int AttemptNumber { get; private set; }
    public string Status { get; private set; } = StatusPending;
    public string Source { get; private set; } = SourceBilling;
    public string? GatewayName { get; private set; }
    public string? GatewayResponseCode { get; private set; }
    public string? FailureReason { get; private set; }
    public string? DeclineClass { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public Guid? DunningCampaignId { get; private set; }
    public Guid? DunningStepId { get; private set; }

#pragma warning disable CS8618
    private ChargeAttemptLog() { }
#pragma warning restore CS8618

    public ChargeAttemptLog(
        Guid subscriptionId,
        DateTime targetBillingDate,
        int attemptNumber,
        string source,
        Guid? dunningCampaignId = null,
        Guid? dunningStepId = null)
    {
        if (attemptNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(attemptNumber), "Attempt number must be at least 1.");
        }

        Id = Guid.CreateVersion7();
        SubscriptionId = subscriptionId;
        TargetBillingDate = targetBillingDate;
        AttemptedAt = DateTime.UtcNow;
        AttemptNumber = attemptNumber;
        Status = StatusPending;
        Source = source.ToUpperInvariant();
        DunningCampaignId = dunningCampaignId;
        DunningStepId = dunningStepId;
    }

    public void MarkSucceeded(string? gatewayName = null, string? gatewayResponseCode = null)
    {
        if (Status == StatusSucceeded)
        {
            return;
        }

        Status = StatusSucceeded;
        GatewayName = gatewayName ?? GatewayName;
        GatewayResponseCode = gatewayResponseCode ?? GatewayResponseCode;
        FailureReason = null;
        DeclineClass = null;
        CompletedAt = DateTime.UtcNow;
    }

    public void MarkFailed(
        string? failureReason = null,
        string? gatewayName = null,
        string? gatewayResponseCode = null,
        string? declineClass = null)
    {
        if (Status == StatusSucceeded)
        {
            return;
        }

        Status = StatusFailed;
        FailureReason = failureReason;
        GatewayName = gatewayName ?? GatewayName;
        GatewayResponseCode = gatewayResponseCode ?? GatewayResponseCode;
        DeclineClass = string.IsNullOrWhiteSpace(declineClass) ? DeclineClass : declineClass.Trim().ToLowerInvariant();
        CompletedAt = DateTime.UtcNow;
    }

    public void MarkSkipped(string? reason = null, string? declineClass = null)
    {
        if (Status == StatusSucceeded)
        {
            return;
        }

        Status = StatusSkipped;
        FailureReason = reason;
        DeclineClass = string.IsNullOrWhiteSpace(declineClass) ? DeclineClass : declineClass.Trim().ToLowerInvariant();
        CompletedAt = DateTime.UtcNow;
    }
}
