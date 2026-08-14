using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.TestSupport;
using Microsoft.EntityFrameworkCore;
using Modules.One.Domain;
using Modules.One.Infrastructure;
using Modules.One.Infrastructure.Workers;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

[TestFixture]
public class OutboundWebhookClaimTests
{
    [Test]
    public void RecordPermanentFailure_FailsImmediately_AttemptCountOne()
    {
        var delivery = new WebhookDeliveryOutbox(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "subscription.activated",
            "{}");

        delivery.RecordPermanentFailure("HTTP 401 Unauthorized");

        delivery.Status.Should().Be("FAILED");
        delivery.AttemptCount.Should().Be(1);
        delivery.LastError.Should().Be("HTTP 401 Unauthorized");
    }

    [Test]
    public void IsPermanentHttpFailure_Treats401And422AsTerminal()
    {
        OutboundWebhookDispatcherJob.IsPermanentHttpFailure(401).Should().BeTrue();
        OutboundWebhookDispatcherJob.IsPermanentHttpFailure(422).Should().BeTrue();
        OutboundWebhookDispatcherJob.IsPermanentHttpFailure(409).Should().BeTrue();
        OutboundWebhookDispatcherJob.IsPermanentHttpFailure(500).Should().BeFalse();
        OutboundWebhookDispatcherJob.IsPermanentHttpFailure(200).Should().BeFalse();
    }

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
        => new(
            InMemoryDb.CreateOptions<OneDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());
}
