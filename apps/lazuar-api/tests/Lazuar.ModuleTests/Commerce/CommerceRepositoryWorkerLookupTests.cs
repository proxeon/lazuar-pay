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

        Assert.That((await repo.GetOrderByIdAsync(order.Id))?.Quantity, Is.EqualTo(3));
        Assert.That((await repo.GetCouponByIdAsync(coupon.Id))?.Id, Is.EqualTo(coupon.Id));
        Assert.That((await repo.GetCheckoutSessionByIdAsync(session.Id))?.Id, Is.EqualTo(session.Id));
        Assert.That(await repo.HasAnyDunningCampaignAsync(orgId), Is.True);
        Assert.That(await repo.HasSubscriptionsAssignedToCampaignAsync(campaign.Id), Is.True);
    }
}
