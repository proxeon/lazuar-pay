using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using FluentAssertions;
using Lazuar.ApiTypes;
using Modules.Commerce.Application;
using Modules.Commerce.Application.Commands;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Contracts.Events;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.Entities;
using Modules.Commerce.Domain.ValueObjects;
using Modules.CRM.Contracts;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class ChosenPriceDiscountTests
{
    [Test]
    public async Task ProcessZeroAmount_YearlyHundredPercentCoupon_DoesNotThrow()
    {
        var orgId = Guid.CreateVersion7();
        var product = DualPriceProduct(orgId);
        var yearly = product.GetPrice("yr")!;
        var coupon = new Coupon(orgId, "FREE100", "PERCENTAGE", 100m, maxUses: 10, expiresAt: null);
        coupon.Reserve();
        var session = new CheckoutSession(
            orgId, Guid.CreateVersion7(), product.Id, coupon.Id, DateTime.UtcNow.AddHours(1), 1, yearly.Id);

        var repository = Substitute.For<ICommerceRepository>();
        repository.GetCheckoutSessionByIdAsync(Arg.Any<Guid>(), session.Id, Arg.Any<CancellationToken>()).Returns(session);
        repository.GetProductByIdAsync(Arg.Any<Guid>(), product.Id, Arg.Any<CancellationToken>()).Returns(product);
        repository.GetCouponByIdAsync(Arg.Any<Guid>(), coupon.Id, Arg.Any<CancellationToken>()).Returns(coupon);

        var handler = new ProcessZeroAmountCheckoutCommandHandler(repository, Substitute.For<IEventBus>());
        await handler.Handle(new ProcessZeroAmountCheckoutCommand(orgId, session.Id), CancellationToken.None);

        session.Status.Should().Be("COMPLETED");
        coupon.UsedCount.Should().Be(1);
    }

    [Test]
    public async Task MarkPaid_YearlyTenPercentCoupon_BooksChosenRowNotCatalogPrice()
    {
        var orgId = Guid.CreateVersion7();
        var product = DualPriceProduct(orgId);
        var yearly = product.GetPrice("yr")!;
        var coupon = new Coupon(orgId, "SAVE10", "PERCENTAGE", 10m, maxUses: 10, expiresAt: null);
        coupon.Reserve();
        var session = new CheckoutSession(
            orgId, Guid.CreateVersion7(), product.Id, coupon.Id, DateTime.UtcNow.AddHours(1), 1, yearly.Id);

        CommerceTransactionLog? log = null;
        var repository = Substitute.For<ICommerceRepository>();
        repository.GetCheckoutSessionByIdAsync(Arg.Any<Guid>(), session.Id, Arg.Any<CancellationToken>()).Returns(session);
        repository.GetProductByIdAsync(Arg.Any<Guid>(), product.Id, Arg.Any<CancellationToken>()).Returns(product);
        repository.GetCouponByIdAsync(Arg.Any<Guid>(), coupon.Id, Arg.Any<CancellationToken>()).Returns(coupon);
        repository.When(r => r.AddTransactionLog(Arg.Any<CommerceTransactionLog>()))
            .Do(ci => log = ci.Arg<CommerceTransactionLog>());

        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(Arg.Any<Guid>(), session.ClientProfileId).Returns(new ClientProfileDto
        {
            Id = session.ClientProfileId.ToString(),
            Full_name = "Buyer",
            Email = "buyer@example.com"
        });

        var handler = new MarkCheckoutAsPaidOfflineCommandHandler(repository, Substitute.For<IEventBus>(), crm);
        await handler.Handle(new MarkCheckoutAsPaidOfflineCommand(orgId, session.Id), CancellationToken.None);

        log.Should().NotBeNull();
        log!.Amount.Should().Be(900m);
    }

    private static Product DualPriceProduct(Guid orgId)
    {
        var product = new Product(
            orgId, "Plan", "plan", 100m, "FIXED", 0m, "MYR", "mo", "BILLPLZ",
            new CheckoutConfiguration(false, false, false),
            new[] { "telegram" });
        product.UpsertPrice("yr", 1000m, isDefault: false);
        return product;
    }
}
