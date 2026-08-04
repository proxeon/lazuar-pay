using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Infrastructure;
using Modules.Commerce.Application;
using Modules.Commerce.Application.Commands;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.Entities;
using Modules.Commerce.Domain.ValueObjects;
using Modules.Commerce.Infrastructure;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.TenantIsolation;

/// <summary>
/// C.9 — Cross-tenant IDOR negative tests on admin command handlers.
/// Handlers must refuse foreign OrganizationId even if the resource id is known.
/// </summary>
[TestFixture]
public class CrossTenantIdorTests
{
    private static Product CreateProduct(Guid orgId) =>
        new(orgId, "Plan", "plan", 10m, "FIXED", 0m, "MYR", "mo", "STRIPE",
            new CheckoutConfiguration(false, false, false), Array.Empty<string>());

    [Test]
    public async Task CancelAdminSubscription_ForeignOrg_ThrowsNotFound()
    {
        var ownerOrg = Guid.CreateVersion7();
        var attackerOrg = Guid.CreateVersion7();
        var product = CreateProduct(ownerOrg);
        var sub = new Subscription(ownerOrg, Guid.CreateVersion7(), product.Id);
        sub.Activate(DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));

        var repository = Substitute.For<ICommerceRepository>();
        repository.GetSubscriptionByIdAsync(sub.Id, Arg.Any<CancellationToken>()).Returns(sub);

        var handler = new CancelAdminSubscriptionCommandHandler(
            repository, Substitute.For<IEventBus>());

        var act = async () => await handler.Handle(
            new CancelAdminSubscriptionCommand(attackerOrg, sub.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
        sub.Status.Should().Be("ACTIVE");
        await repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RecordRefund_ForeignOrg_ThrowsNotFound()
    {
        var ownerOrg = Guid.CreateVersion7();
        var attackerOrg = Guid.CreateVersion7();
        var log = new CommerceTransactionLog(
            organizationId: ownerOrg,
            amount: 50m,
            feeAmount: 0m,
            currency: "MYR",
            status: "SUCCEEDED",
            customerName: "Alice",
            customerEmail: "a@b.com",
            productName: "Plan",
            recordedByName: "SYSTEM",
            externalReference: "pi_idor");

        var repository = Substitute.For<ICommerceRepository>();
        repository.GetTransactionLogByIdAsync(log.Id, Arg.Any<CancellationToken>()).Returns(log);

        var handler = new RecordRefundCommandHandler(repository, Substitute.For<IEventBus>());

        var act = async () => await handler.Handle(
            new RecordRefundCommand(attackerOrg, log.Id, Amount: null), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
        await repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateCoupon_ForeignOrg_ThrowsNotFound()
    {
        var ownerOrg = Guid.CreateVersion7();
        var attackerOrg = Guid.CreateVersion7();
        var coupon = new Coupon(ownerOrg, "SAVE", "PERCENTAGE", 10m, 100, null);

        var repository = Substitute.For<ICommerceRepository>();
        repository.GetCouponByIdAsync(coupon.Id, Arg.Any<CancellationToken>()).Returns(coupon);

        var handler = new UpdateCouponCommandHandler(repository);

        var act = async () => await handler.Handle(
            new UpdateCouponCommand(
                attackerOrg,
                coupon.Id,
                Code: "HACKED",
                DiscountType: null,
                Amount: null,
                MaxUses: null,
                MinimumOriginalPrice: null,
                ExpiresAt: null,
                ApplicableProductIds: null,
                IsActive: null),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
        coupon.Code.Should().Be("SAVE");
    }

    [Test]
    public async Task DeleteCoupon_ForeignOrg_ThrowsNotFound()
    {
        var ownerOrg = Guid.CreateVersion7();
        var attackerOrg = Guid.CreateVersion7();
        var coupon = new Coupon(ownerOrg, "SAVE", "PERCENTAGE", 10m, 100, null);

        var repository = Substitute.For<ICommerceRepository>();
        repository.GetCouponByIdAsync(coupon.Id, Arg.Any<CancellationToken>()).Returns(coupon);

        var handler = new DeleteCouponCommandHandler(repository);

        var act = async () => await handler.Handle(
            new DeleteCouponCommand(attackerOrg, coupon.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
        coupon.IsActive.Should().BeTrue();
    }

    [Test]
    public async Task BillingLedger_AmbientTenantFilter_HidesOtherOrgRows()
    {
        var orgA = Guid.CreateVersion7();
        var orgB = Guid.CreateVersion7();

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var executionContext = Substitute.For<IExecutionContextAccessor>();
        executionContext.TenantId.Returns(orgA);

        await using var db = new BillingDbContext(
            options,
            executionContext,
            Substitute.For<IMediator>(),
            new DatabaseJobTrigger());

        var entryA = new LedgerEntry(orgA, "GATEWAY_PAYMENT", "tx_a", "a", "B2C");
        entryA.AddLine("ASSET_CASH", 10m, "MYR", 10m, "MYR");
        entryA.AddLine("REVENUE_GROSS", -10m, "MYR", -10m, "MYR");
        entryA.ValidateBalanced();

        var entryB = new LedgerEntry(orgB, "GATEWAY_PAYMENT", "tx_b", "b", "B2C");
        entryB.AddLine("ASSET_CASH", 20m, "MYR", 20m, "MYR");
        entryB.AddLine("REVENUE_GROSS", -20m, "MYR", -20m, "MYR");
        entryB.ValidateBalanced();

        db.LedgerEntries.Add(entryA);
        db.LedgerEntries.Add(entryB);
        await db.SaveChangesAsync();

        var visible = await db.LedgerEntries.ToListAsync();
        visible.Should().HaveCount(1);
        visible[0].OrganizationId.Should().Be(orgA);

        var all = await db.LedgerEntries.IgnoreQueryFilters().ToListAsync();
        all.Should().HaveCount(2);
    }
}
