using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Domain;
using FluentAssertions;
using Modules.Commerce.Application;
using Modules.Commerce.Application.Queries;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.ValueObjects;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class ValidateCouponQueryHandlerTests
{
    [Test]
    public async Task YearlyFixedCoupon_Qty3_ReturnsLineDiscountAgainstChosenRow()
    {
        var (handler, product, _) = Setup("FIXED", 30m);

        var result = await handler.Handle(
            new ValidateCouponQuery(
                product.OrganizationId, product.Slug, "SAVE30",
                PriceId: product.GetPrice("yr")!.Id,
                Interval: "yr",
                Quantity: 3),
            CancellationToken.None);

        result.Is_valid.Should().BeTrue();
        result.Discount_amount.Should().Be(90);
        result.Final_price.Should().Be(2910);
    }

    [Test]
    public async Task YearlyMinOriginal_PassesWhenCatalogMonthlyWouldFail()
    {
        var (handler, product, _) = Setup("FIXED", 30m, minimumOriginalPrice: 500m);

        var result = await handler.Handle(
            new ValidateCouponQuery(
                product.OrganizationId, product.Slug, "SAVE30",
                Interval: "yr",
                Quantity: 1),
            CancellationToken.None);

        result.Is_valid.Should().BeTrue();
        result.Discount_amount.Should().Be(30);
        result.Final_price.Should().Be(970);
    }

    [Test]
    public async Task MonthlyMinOriginal_StillFailsWhenChosenRowIsBelowFloor()
    {
        var (handler, product, _) = Setup("FIXED", 30m, minimumOriginalPrice: 500m);

        var act = async () => await handler.Handle(
            new ValidateCouponQuery(
                product.OrganizationId, product.Slug, "SAVE30",
                Interval: "mo",
                Quantity: 1),
            CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleValidationException>()
            .WithMessage("*minimum original price*");
    }

    [Test]
    public async Task DefaultArgs_StillDiscountCatalogPrice()
    {
        var (handler, product, _) = Setup("PERCENTAGE", 10m);

        var result = await handler.Handle(
            new ValidateCouponQuery(product.OrganizationId, product.Slug, "SAVE30"),
            CancellationToken.None);

        result.Discount_amount.Should().Be(10);
        result.Final_price.Should().Be(90);
    }

    private static (ValidateCouponQueryHandler Handler, Product Product, Coupon Coupon) Setup(
        string discountType,
        decimal amount,
        decimal minimumOriginalPrice = 0)
    {
        var orgId = Guid.CreateVersion7();
        var product = new Product(
            orgId, "Plan", "plan", 100m, "FIXED", 0m, "MYR", "mo", "BILLPLZ",
            new CheckoutConfiguration(false, false, false),
            new[] { "telegram" });
        product.UpsertPrice("yr", 1000m, isDefault: false);
        var coupon = new Coupon(orgId, "SAVE30", discountType, amount, 10, null, minimumOriginalPrice);

        var repository = Substitute.For<ICommerceRepository>();
        repository.GetProductBySlugAsync(orgId, product.Slug, Arg.Any<CancellationToken>()).Returns(product);
        repository.GetCouponByCodeAsync(orgId, "SAVE30", Arg.Any<CancellationToken>()).Returns(coupon);

        return (new ValidateCouponQueryHandler(repository), product, coupon);
    }
}
