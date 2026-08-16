using System;
using Modules.Commerce.Domain.Aggregates;

namespace Modules.Commerce.Application;

public static class SubscriptionBillingAmount
{
    public static decimal Unit(Subscription sub, Product product)
    {
        ArgumentNullException.ThrowIfNull(sub);
        ArgumentNullException.ThrowIfNull(product);
        return sub.UnitAmount > 0 ? sub.UnitAmount : product.Price;
    }

    public static int Seats(Subscription sub)
    {
        ArgumentNullException.ThrowIfNull(sub);
        return Math.Max(1, sub.Quantity);
    }

    public static decimal Line(Subscription sub, Product product) =>
        Unit(sub, product) * Seats(sub);

    public static DateTime AdvanceFrom(DateTime from, string? interval) =>
        string.Equals(interval, "yr", StringComparison.OrdinalIgnoreCase)
            ? from.AddYears(1)
            : from.AddMonths(1);

    public static string ResolveInterval(Subscription sub, Product product)
    {
        if (!string.IsNullOrWhiteSpace(sub.BillingInterval))
        {
            return sub.BillingInterval;
        }

        return product.Interval;
    }
}
