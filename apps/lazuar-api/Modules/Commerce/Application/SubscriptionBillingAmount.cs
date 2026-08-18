using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Modules.Billing.Contracts;
using Modules.Commerce.Domain.Aggregates;

namespace Modules.Commerce.Application;

public static class SubscriptionBillingAmount
{
    public readonly record struct Breakdown(
        decimal UnitNet,
        decimal UnitTax,
        decimal UnitGross,
        int Seats,
        decimal Gross,
        string TaxType);

    public static decimal Unit(Subscription sub, Product product)
    {
        ArgumentNullException.ThrowIfNull(sub);
        ArgumentNullException.ThrowIfNull(product);
        return ResolveUnit(sub.HasUnitSnapshot, sub.UnitAmount, product.Price);
    }

    public static decimal ResolveUnit(bool hasSnapshot, decimal unitAmount, decimal fallbackUnit) =>
        hasSnapshot ? unitAmount : (unitAmount > 0 ? unitAmount : fallbackUnit);

    public static int Seats(Subscription sub)
    {
        ArgumentNullException.ThrowIfNull(sub);
        return Math.Max(1, sub.Quantity);
    }

    public static decimal Line(Subscription sub, Product product) =>
        Unit(sub, product) * Seats(sub);

    public const decimal DefaultServiceTaxRatePercent = 8m;

    public static Breakdown CustomQuoteBreakdown(decimal net, bool merchantHasSst) =>
        GrossBreakdown(net, 1, SstTaxMath.ServiceTax, DefaultServiceTaxRatePercent, merchantHasSst);

    /// <summary>
    /// Exclusive SST is rounded on the unit, then × seats (B01-C12 / B02-C20).
    /// Hop-2 adapters multiply <c>Amount × Quantity</c>, so the charged line is
    /// <c>unitGross * seats</c>. Do not switch this helper to tax(<c>unitNet * seats</c>)
    /// without also changing the adapter contract — that mix is a sen off on odd prices.
    /// </summary>
    public static Breakdown GrossBreakdown(
        decimal unitNet,
        int seats,
        string? sstTaxType,
        decimal sstRatePercent,
        bool merchantHasSst)
    {
        seats = Math.Max(1, seats);
        var (taxType, unitTax) = SstTaxMath.Compute(sstTaxType, sstRatePercent, unitNet, merchantHasSst);
        var unitGross = unitNet + unitTax;
        return new Breakdown(unitNet, unitTax, unitGross, seats, unitGross * seats, taxType);
    }

    public static Breakdown GrossBreakdown(Subscription sub, Product product, bool merchantHasSst)
    {
        ArgumentNullException.ThrowIfNull(sub);
        ArgumentNullException.ThrowIfNull(product);
        return GrossBreakdown(Unit(sub, product), Seats(sub), product.SstTaxType, product.SstRatePercent, merchantHasSst);
    }

    public static decimal Gross(
        decimal unitNet,
        int seats,
        string? sstTaxType,
        decimal sstRatePercent,
        bool merchantHasSst) =>
        GrossBreakdown(unitNet, seats, sstTaxType, sstRatePercent, merchantHasSst).Gross;

    public static decimal Gross(Subscription sub, Product product, bool merchantHasSst) =>
        GrossBreakdown(sub, product, merchantHasSst).Gross;

    public static decimal LineTax(Breakdown breakdown) => breakdown.UnitTax * breakdown.Seats;

    /// <summary>
    /// Clerk/offline cash is the inclusive gross. Extract exclusive SST so
    /// Billing can split LIABILITY_TAX_PAYABLE without changing cash collected.
    /// </summary>
    public static decimal TaxFromInclusiveGross(
        decimal gross,
        bool merchantHasSst,
        string? sstTaxType,
        decimal sstRatePercent)
    {
        if (!merchantHasSst
            || sstRatePercent <= 0
            || gross <= 0
            || !string.Equals(sstTaxType, SstTaxMath.ServiceTax, StringComparison.OrdinalIgnoreCase))
        {
            return 0m;
        }

        var net = Math.Round(gross / (1m + sstRatePercent / 100m), 2, MidpointRounding.AwayFromZero);
        return Math.Max(0m, gross - net);
    }

    public static void StampSstMetadata(IDictionary<string, string> metadata, Breakdown breakdown)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        var tax = LineTax(breakdown);
        if (tax <= 0)
        {
            return;
        }

        metadata["sst_tax_amount"] = tax.ToString("0.00", CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(breakdown.TaxType))
        {
            metadata["sst_tax_type"] = breakdown.TaxType;
        }
    }

    public static async Task<bool> MerchantHasSstAsync(IBillingQueryService? billing, Guid organizationId)
    {
        if (billing == null)
        {
            throw new InvalidOperationException(
                "IBillingQueryService is required to decide SST; refusing to undercharge.");
        }

        var profile = await billing.GetBillingProfileAsync(organizationId);
        return !string.IsNullOrWhiteSpace(profile?.Sst_registration_number);
    }

    public static async Task<Breakdown> GrossBreakdown(
        Subscription sub,
        Product product,
        IBillingQueryService? billing)
    {
        var merchantHasSst = await MerchantHasSstAsync(billing, sub.OrganizationId);
        return GrossBreakdown(sub, product, merchantHasSst);
    }

    public static async Task<decimal> Gross(
        Subscription sub,
        Product product,
        IBillingQueryService? billing)
    {
        var breakdown = await GrossBreakdown(sub, product, billing);
        return breakdown.Gross;
    }

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
