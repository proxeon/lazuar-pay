using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.ApiTypes;
using Lazuar.TestSupport;
using Modules.Billing.Contracts;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.ValueObjects;
using Modules.Commerce.Infrastructure;
using Modules.Commerce.Infrastructure.Services;
using Modules.CRM.Contracts;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class SubscriberQueryServiceMailContextTests
{
    [Test]
    public async Task GetSubscriptionMailContext_ReturnsProductAndPeriod()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var product = new Product(
            orgId,
            "Premium",
            "premium",
            99.00m,
            "FIXED",
            0m,
            "MYR",
            "mo",
            "STRIPE",
            new CheckoutConfiguration(false, false, false),
            Array.Empty<string>());
        product.SetSst("02", 8m);
        var sub = new Subscription(orgId, Guid.CreateVersion7(), product.Id);
        var periodEnd = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        sub.Activate(periodEnd, periodEnd, quantity: 5, unitAmount: 99.00m);
        db.Products.Add(product);
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        var billing = Substitute.For<IBillingQueryService>();
        billing.GetBillingProfileAsync(orgId).Returns(new TenantBillingProfileDto
        {
            Legal_name = "Acme",
            Tin = "C12345678901",
            Sst_registration_number = "W10-1234-12345678"
        });

        var svc = new SubscriberQueryService(
            Substitute.For<ISqlConnectionFactory>(),
            Substitute.For<ICrmQueryService>(),
            db,
            billing);

        var ctx = await svc.GetSubscriptionMailContextAsync(orgId, sub.Id);

        ctx.Should().NotBeNull();
        ctx!.SubscriptionId.Should().Be(sub.Id);
        ctx.ProductId.Should().Be(product.Id);
        ctx.PlanName.Should().Be("Premium");
        ctx.Price.Should().Be(534.60m);
        ctx.Currency.Should().Be("MYR");
        ctx.NextBillingDate.Should().Be(periodEnd);
        ctx.Status.Should().Be("ACTIVE");
    }

    [Test]
    public async Task GetSubscriptionMailContext_OrgMismatch_ReturnsNull()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var product = new Product(
            orgId,
            "Premium",
            "premium",
            99.00m,
            "FIXED",
            0m,
            "MYR",
            "mo",
            "STRIPE",
            new CheckoutConfiguration(false, false, false),
            Array.Empty<string>());
        var sub = new Subscription(orgId, Guid.CreateVersion7(), product.Id);
        db.Products.Add(product);
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        var svc = new SubscriberQueryService(
            Substitute.For<ISqlConnectionFactory>(),
            Substitute.For<ICrmQueryService>(),
            db);

        var ctx = await svc.GetSubscriptionMailContextAsync(Guid.CreateVersion7(), sub.Id);

        ctx.Should().BeNull();
    }

    private static CommerceDbContext CreateDb() =>
        new(
            InMemoryDb.CreateOptions<CommerceDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());
}
