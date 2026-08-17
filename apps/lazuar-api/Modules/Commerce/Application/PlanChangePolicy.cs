using System;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Domain.Aggregates;

namespace Modules.Commerce.Application;

/// <summary>
/// Next-renewal-only catalog change (LP-059). Never charges mid-cycle.
/// </summary>
public static class PlanChangePolicy
{
    public const string NextRenewal = "next_renewal";

    public static PlanChangePreview Preview(Subscription sub, Product currentProduct, Product targetProduct, int quantity)
    {
        ArgumentNullException.ThrowIfNull(sub);
        ArgumentNullException.ThrowIfNull(currentProduct);
        ArgumentNullException.ThrowIfNull(targetProduct);

        var qty = Math.Max(1, quantity);
        var currentUnit = sub.UnitAmount > 0 ? sub.UnitAmount : currentProduct.Price;
        var nextUnit = ResolveTargetPrice(sub, currentProduct, targetProduct).Unit;

        return new PlanChangePreview(
            sub.ProductId,
            currentUnit * Math.Max(1, sub.Quantity),
            currentProduct.Currency,
            currentProduct.Interval,
            targetProduct.Id,
            nextUnit * qty,
            sub.NextBillingDate,
            AmountDueNow: 0m,
            Policy: NextRenewal);
    }

    public static void RejectImmediateOrProrate(bool? prorate, string? apply)
    {
        if (prorate == true)
        {
            throw new InvalidOperationException("Proration is not supported. Changes take effect at the next renewal.");
        }

        if (!string.IsNullOrWhiteSpace(apply)
            && apply.Equals("immediate", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Immediate apply is not supported. Changes take effect at the next renewal.");
        }
    }

    public static void GuardLiveStatus(Subscription sub)
    {
        if (sub.Status is not ("ACTIVE" or "TRIALING"))
        {
            throw new InvalidOperationException($"Cannot change plan from status '{sub.Status}'.");
        }
    }

    public static void GuardTargetProduct(Subscription sub, Product current, Product target)
    {
        if (target.OrganizationId != sub.OrganizationId)
        {
            throw new InvalidOperationException("Target product not found.");
        }

        if (!target.IsActive)
        {
            throw new InvalidOperationException("Target product is not active.");
        }

        if (target.Interval is not ("mo" or "yr"))
        {
            throw new InvalidOperationException("Target product must be a recurring monthly or yearly plan.");
        }

        if (!string.Equals(target.GatewayName, current.GatewayName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Plan change must stay on the same payment gateway.");
        }

        if (!string.Equals(target.Currency, current.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Plan change must stay on the same currency.");
        }

        _ = ResolveTargetPrice(sub, current, target);
    }

    public static (decimal Unit, string Interval, Guid? PriceId) ResolveTargetPrice(
        Subscription sub,
        Product current,
        Product target)
    {
        ArgumentNullException.ThrowIfNull(sub);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(target);

        var interval = SubscriptionBillingAmount.ResolveInterval(sub, current);
        if (TryResolvePrice(target, interval, out var amount, out var billedInterval, out var priceId))
        {
            return (amount, billedInterval, priceId);
        }

        throw new InvalidOperationException("Interval change requires a new checkout.");
    }

    public static bool TryResolvePrice(
        Product product,
        string interval,
        out decimal amount,
        out string billedInterval,
        out Guid? priceId)
    {
        ArgumentNullException.ThrowIfNull(product);
        var row = product.GetPrice(interval);
        if (row != null)
        {
            amount = row.Amount;
            billedInterval = row.Interval;
            priceId = row.Id;
            return true;
        }

        if (string.Equals(product.Interval, interval, StringComparison.OrdinalIgnoreCase))
        {
            amount = product.Price;
            billedInterval = product.Interval;
            priceId = product.DefaultPrice()?.Id;
            return true;
        }

        amount = 0;
        billedInterval = interval;
        priceId = null;
        return false;
    }
}
