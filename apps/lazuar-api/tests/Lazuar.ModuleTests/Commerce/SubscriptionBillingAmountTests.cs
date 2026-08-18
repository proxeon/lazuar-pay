using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Lazuar.ApiTypes;
using Modules.Billing.Contracts;
using Modules.Commerce.Application;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.ValueObjects;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class SubscriptionBillingAmountTests
{
    [Test]
    public async Task Gross_SstRegistered_Unit100_Rate8_Is108()
    {
        var (sub, product) = Create(unitAmount: 100m, quantity: 1);
        var billing = SstBilling(sub.OrganizationId, "W10-1234-12345678");

        (await SubscriptionBillingAmount.Gross(sub, product, billing)).Should().Be(108m);

        var breakdown = await SubscriptionBillingAmount.GrossBreakdown(sub, product, billing);
        breakdown.UnitNet.Should().Be(100m);
        breakdown.UnitTax.Should().Be(8m);
        breakdown.UnitGross.Should().Be(108m);
        breakdown.Seats.Should().Be(1);
        breakdown.Gross.Should().Be(108m);
        breakdown.TaxType.Should().Be("02");
        sub.UnitAmount.Should().Be(100m);
    }

    [Test]
    public async Task Gross_SstRegistered_Qty3_Is324()
    {
        var (sub, product) = Create(unitAmount: 100m, quantity: 3);
        var billing = SstBilling(sub.OrganizationId, "W10-1234-12345678");

        (await SubscriptionBillingAmount.Gross(sub, product, billing)).Should().Be(324m);
        (await SubscriptionBillingAmount.GrossBreakdown(sub, product, billing)).Gross.Should().Be(324m);
        SubscriptionBillingAmount.Line(sub, product).Should().Be(300m);
        sub.UnitAmount.Should().Be(100m);
    }

    [Test]
    public void GrossBreakdown_PerUnitThenSeats_PinsSenSplit()
    {
        // 10.03 × 8% = 0.8024 → 0.80; × 3 = 2.40; gross 32.49.
        // Line-level tax(30.09 × 8%) = 2.41 / 32.50 — not the hop-2 SSoT.
        var odd = SubscriptionBillingAmount.GrossBreakdown(10.03m, 3, "02", 8m, merchantHasSst: true);
        odd.UnitTax.Should().Be(0.80m);
        odd.Gross.Should().Be(32.49m);
        SubscriptionBillingAmount.LineTax(odd).Should().Be(2.40m);

        // 33.33 × 8% = 2.6664 → 2.67; × 3 = 8.01. Line-level would be 8.00.
        var seats = SubscriptionBillingAmount.GrossBreakdown(33.33m, 3, "02", 8m, merchantHasSst: true);
        seats.UnitTax.Should().Be(2.67m);
        SubscriptionBillingAmount.LineTax(seats).Should().Be(8.01m);
        seats.Gross.Should().Be(108.00m);
    }

    [Test]
    public void Unit_WrittenZeroSnapshot_IsZeroNotCatalog()
    {
        var orgId = Guid.CreateVersion7();
        var catalog = new Product(
            orgId, "Paid", "paid", 100m, "FIXED", 0m, "MYR", "mo", "STRIPE",
            new CheckoutConfiguration(false, false, false), Array.Empty<string>());
        var free = new Subscription(orgId, Guid.CreateVersion7(), catalog.Id);
        free.Activate(DateTime.UtcNow, DateTime.UtcNow.AddMonths(1), false, 3, 0m);

        SubscriptionBillingAmount.Unit(free, catalog).Should().Be(0m);
        SubscriptionBillingAmount.Gross(free, catalog, merchantHasSst: false).Should().Be(0m);
    }

    [Test]
    public void Unit_MissingSnapshot_FallsBackToCatalog()
    {
        var orgId = Guid.CreateVersion7();
        var catalog = new Product(
            orgId, "Paid", "paid", 100m, "FIXED", 0m, "MYR", "mo", "STRIPE",
            new CheckoutConfiguration(false, false, false), Array.Empty<string>());
        var pending = new Subscription(orgId, Guid.CreateVersion7(), catalog.Id);

        SubscriptionBillingAmount.Unit(pending, catalog).Should().Be(100m);
    }

    [Test]
    public async Task Gross_NoSst_Is100()
    {
        var (sub, product) = Create(unitAmount: 100m, quantity: 1);

        (await SubscriptionBillingAmount.Gross(sub, product, SstBilling(sub.OrganizationId, sstNumber: ""))).Should().Be(100m);
        SubscriptionBillingAmount.Line(sub, product).Should().Be(100m);
        sub.UnitAmount.Should().Be(100m);
    }

    [Test]
    public async Task StampSstMetadata_SstRegistered_WritesAmountAndType()
    {
        var (sub, product) = Create(unitAmount: 100m, quantity: 1);
        var billing = SstBilling(sub.OrganizationId, "W10-1234-12345678");
        var breakdown = await SubscriptionBillingAmount.GrossBreakdown(sub, product, billing);
        var metadata = new Dictionary<string, string>();

        SubscriptionBillingAmount.StampSstMetadata(metadata, breakdown);

        metadata["sst_tax_amount"].Should().Be("8.00");
        metadata["sst_tax_type"].Should().Be("02");
    }

    private static (Subscription Sub, Product Product) Create(decimal unitAmount, int quantity)
    {
        var orgId = Guid.CreateVersion7();
        var product = new Product(
            orgId,
            "Plan",
            $"plan-{Guid.CreateVersion7():N}"[..20],
            unitAmount,
            "FIXED",
            0m,
            "MYR",
            "mo",
            "STRIPE",
            new CheckoutConfiguration(false, false, false),
            Array.Empty<string>());
        product.SetSst("02", 8m);

        var sub = new Subscription(orgId, Guid.CreateVersion7(), product.Id);
        sub.Activate(DateTime.UtcNow, DateTime.UtcNow.AddMonths(1), false, quantity, unitAmount);
        return (sub, product);
    }

    [Test]
    public async Task MerchantHasSst_Null_Billing_Throws()
    {
        var act = () => SubscriptionBillingAmount.MerchantHasSstAsync(null, Guid.CreateVersion7());
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*refusing to undercharge*");
    }

    private static IBillingQueryService SstBilling(Guid organizationId, string sstNumber)
    {
        var billing = Substitute.For<IBillingQueryService>();
        billing.GetBillingProfileAsync(organizationId).Returns(new TenantBillingProfileDto
        {
            Legal_name = "Acme",
            Tin = "C12345678901",
            Sst_registration_number = sstNumber
        });
        return billing;
    }
}
