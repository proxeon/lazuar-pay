using System;
using Modules.Commerce.Domain.Aggregates;

namespace Modules.Commerce.Application;

public static class SubscriptionActivation
{
    public static bool IsTrialOffer(Product product) =>
        product.TrialDays > 0 && product.Interval is "mo" or "yr";

    public static void Start(
        Subscription subscription,
        Product product,
        int quantity,
        decimal unitAmount,
        bool reminderOnly,
        string? billingInterval = null,
        Guid? priceId = null,
        DateTime? now = null)
    {
        var instant = now ?? DateTime.UtcNow;
        var interval = string.IsNullOrWhiteSpace(billingInterval) ? product.Interval : billingInterval;

        if (IsTrialOffer(product))
        {
            subscription.ActivateTrial(instant.AddDays(product.TrialDays), reminderOnly, quantity, unitAmount);
        }
        else
        {
            var next = SubscriptionBillingAmount.AdvanceFrom(instant, interval);
            subscription.Activate(instant, next, reminderOnly, quantity, unitAmount);
        }

        subscription.SetBillingInterval(interval);
        if (priceId.HasValue)
        {
            subscription.SetPriceId(priceId);
        }
    }
}
