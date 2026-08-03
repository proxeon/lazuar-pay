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
}
