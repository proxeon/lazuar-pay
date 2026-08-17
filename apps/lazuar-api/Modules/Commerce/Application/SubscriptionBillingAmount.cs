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
        return sub.UnitAmount > 0 ? sub.UnitAmount : product.Price;
    }

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
            return false;
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
