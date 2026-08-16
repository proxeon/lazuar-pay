using System;
using Modules.Commerce.Domain.Aggregates;

namespace Modules.Commerce.Application;

internal static class SubscriptionCancelDecision
{
    internal enum Outcome
    {
        AlreadyCanceled,
        Scheduled,
        ImmediateCanceled
    }

    internal static Outcome Apply(Subscription subscription, bool atPeriodEnd)
    {
        if (subscription.Status == "CANCELED")
        {
            return Outcome.AlreadyCanceled;
        }

        if (subscription.Status is not ("ACTIVE" or "PAST_DUE" or "SUSPENDED" or "TRIALING"))
        {
            throw new InvalidOperationException(
                $"Subscription cannot be canceled from status '{subscription.Status}'.");
        }

        if (atPeriodEnd)
        {
            if (subscription.CancelAtPeriodEnd)
            {
                return Outcome.Scheduled;
            }

            if (subscription.Status is "ACTIVE" or "TRIALING"
                && subscription.NextBillingDate is { } next
                && next > DateTime.UtcNow)
            {
                subscription.ScheduleCancelAtPeriodEnd();
                return Outcome.Scheduled;
            }
        }

        subscription.Cancel();
        return Outcome.ImmediateCanceled;
    }
}
