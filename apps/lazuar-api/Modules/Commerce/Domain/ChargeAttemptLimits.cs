namespace Modules.Commerce.Domain;

/// <summary>
/// Hard limits for off-session charge retries within a single billing cycle
/// (same subscription + target billing date).
/// </summary>
public static class ChargeAttemptLimits
{
    /// <summary>
    /// Maximum charge attempts per (SubscriptionId, TargetBillingDate), including the initial billing attempt.
    /// Billing owns attempt 1; dunning AUTO_CHARGE owns attempts 2–Max.
    /// </summary>
    public const int MaxAttemptsPerBillingCycle = 4;

    /// <summary>
    /// PENDING rows older than this are treated as a lost webhook (B03-C21) so AUTO_CHARGE can retry.
    /// Fresh PENDING still defers.
    /// </summary>
    public static readonly TimeSpan PendingTimeout = TimeSpan.FromHours(24);
}
