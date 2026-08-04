using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.One.Domain;
using Modules.One.Infrastructure;
using Modules.One.Infrastructure.Workers;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

[TestFixture]
public class OutboundWebhookClaimTests
{
    [Test]
    public void ClaimLease_PushesNextAttemptAt_WhilePending()
    {
        var delivery = new WebhookDeliveryOutbox(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "subscription.activated",
            """{"event_type":"subscription.activated"}""");

        var original = delivery.NextAttemptAt;
        var leaseUntil = DateTime.UtcNow.AddMinutes(2);
        delivery.ClaimLease(leaseUntil);

        delivery.Status.Should().Be("PENDING");
        delivery.NextAttemptAt.Should().BeCloseTo(leaseUntil, TimeSpan.FromSeconds(1));
        delivery.NextAttemptAt.Should().BeAfter(original.AddSeconds(-1));
    }

    [Test]
    public void ClaimLease_IgnoresNonPending()
    {
        var delivery = new WebhookDeliveryOutbox(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "order.completed",
            "{}");
        delivery.RecordSuccess();
        var at = delivery.NextAttemptAt;

        delivery.ClaimLease(DateTime.UtcNow.AddHours(1));
        delivery.NextAttemptAt.Should().Be(at);
    }

    [Test]
    public async Task ClaimPendingDeliveries_InMemory_LeasesEligibleRows()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var endpointId = Guid.CreateVersion7();

        var due = new WebhookDeliveryOutbox(orgId, endpointId, "subscription.activated", "{}");
        var future = new WebhookDeliveryOutbox(orgId, endpointId, "subscription.activated", "{}");
        // Bump future delivery beyond now via ClaimLease misuse as schedule.
        future.ClaimLease(DateTime.UtcNow.AddHours(1));

        db.WebhookDeliveryOutboxes.AddRange(due, future);
        await db.SaveChangesAsync();

        var leaseUntil = DateTime.UtcNow.AddMinutes(2);
        var claimed = await OutboundWebhookDispatcherJob.ClaimPendingDeliveriesAsync(
            db, leaseUntil, CancellationToken.None);

        claimed.Should().ContainSingle(d => d.Id == due.Id);
        claimed.Should().NotContain(d => d.Id == future.Id);

        var reloaded = await db.WebhookDeliveryOutboxes.IgnoreQueryFilters().SingleAsync(d => d.Id == due.Id);
        reloaded.NextAttemptAt.Should().BeCloseTo(leaseUntil, TimeSpan.FromSeconds(2));
    }

    private static OneDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<OneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var ctx = Substitute.For<IExecutionContextAccessor>();
        ctx.TenantId.Returns(Guid.Empty);
        return new OneDbContext(options, ctx, Substitute.For<IMediator>(), new DatabaseJobTrigger());
    }
}
