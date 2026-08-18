using System;
using System.Threading.Tasks;
using BuildingBlocks.Infrastructure;
using Lazuar.TestSupport;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Infrastructure;
using Modules.Commerce.Infrastructure.Repositories;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class CommerceRepositoryWorkerLookupTests
{
    [Test]
    public async Task Id_Lookups_See_Rows_When_Ambient_Tenant_Is_Empty()
    {
        var orgId = Guid.CreateVersion7();
        var options = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var ctx = new FakeExecutionContextAccessor { TenantId = Guid.Empty };
        await using var db = new CommerceDbContext(options, ctx, Substitute.For<IMediator>(), new DatabaseJobTrigger());
        var repo = new CommerceRepository(db);

        var order = new Order(orgId, Guid.CreateVersion7(), Guid.CreateVersion7(), 30m, "MYR", quantity: 3);
        var coupon = new Coupon(orgId, "SAVE10", "PERCENTAGE", 10m, maxUses: 5, expiresAt: null);
        var session = new CheckoutSession(orgId, Guid.CreateVersion7(), Guid.CreateVersion7(), coupon.Id, DateTime.UtcNow.AddHours(1));
        var campaign = new DunningCampaign(orgId, "Default", "CANCEL", gracePeriodDays: 7);
        var sub = new Subscription(orgId, Guid.CreateVersion7(), Guid.CreateVersion7());
        sub.MarkAsPastDue();
        sub.AssignDunningCampaign(campaign.Id);

        db.Orders.Add(order);
        db.Coupons.Add(coupon);
        db.CheckoutSessions.Add(session);
        db.DunningCampaigns.Add(campaign);
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        Assert.That(await db.Orders.CountAsync(), Is.EqualTo(0), "empty ambient must hide filtered sets");

        Assert.That((await repo.GetOrderByIdAsync(orgId, order.Id))?.Quantity, Is.EqualTo(3));
        Assert.That((await repo.GetCouponByIdAsync(orgId, coupon.Id))?.Id, Is.EqualTo(coupon.Id));
        Assert.That((await repo.GetCheckoutSessionByIdAsync(orgId, session.Id))?.Id, Is.EqualTo(session.Id));
        Assert.That(await repo.HasAnyDunningCampaignAsync(orgId), Is.True);
        Assert.That(await repo.HasSubscriptionsAssignedToCampaignAsync(orgId, campaign.Id), Is.True);
        Assert.That(await repo.GetOrderByIdAsync(Guid.CreateVersion7(), order.Id), Is.Null);
    }

    [Test]
    public async Task NewestSubscriptionForClient_PrefersLiveOverNewerCanceled_AndSkipsPending()
    {
        var orgId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var options = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var ctx = new FakeExecutionContextAccessor { TenantId = Guid.Empty };
        await using var db = new CommerceDbContext(options, ctx, Substitute.For<IMediator>(), new DatabaseJobTrigger());
        var repo = new CommerceRepository(db);

        var live = new Subscription(orgId, clientId, Guid.CreateVersion7());
        live.Activate(DateTime.UtcNow, DateTime.UtcNow.AddDays(30));
        var canceled = new Subscription(orgId, clientId, Guid.CreateVersion7());
        canceled.Activate(DateTime.UtcNow, DateTime.UtcNow.AddDays(30));
        canceled.Cancel();
        var pending = new Subscription(orgId, clientId, Guid.CreateVersion7());

        db.Subscriptions.AddRange(live, canceled, pending);
        await db.SaveChangesAsync();

        var newest = await repo.GetNewestSubscriptionForClientAsync(orgId, clientId);
        Assert.That(newest, Is.Not.Null);
        Assert.That(newest!.Id, Is.EqualTo(live.Id));
        Assert.That(newest.Status, Is.EqualTo("ACTIVE"));
    }
}
