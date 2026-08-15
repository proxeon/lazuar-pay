using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.TestSupport;
using Microsoft.EntityFrameworkCore;
using Modules.One.Application.Commands;
using Modules.One.Domain;
using Modules.One.Infrastructure;
using Modules.One.Infrastructure.Repositories;
using Modules.One.Infrastructure.Workers;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

[TestFixture]
public class RedeliverWebhookDeliveryTests
{
    [Test]
    public async Task Redeliver_Failed_InsertsPendingClone_SamePayload_NewId()
    {
        await using var db = CreateDb();
        var repo = new OneRepository(db);
        var (orgId, endpoint, source) = await SeedAsync(db, terminal: "FAILED");
        var payload = source.Payload;
        var sourceId = source.Id;

        var result = await new RedeliverWebhookDeliveryCommandHandler(repo)
            .Handle(new RedeliverWebhookDeliveryCommand(orgId, sourceId), CancellationToken.None);

        var rows = await db.WebhookDeliveryOutboxes.IgnoreQueryFilters().ToListAsync();
        rows.Should().HaveCount(2);

        var original = rows.Single(d => d.Id == sourceId);
        original.Status.Should().Be("FAILED");
        original.Payload.Should().Be(payload);

        var clone = rows.Single(d => d.Id == result.Id);
        clone.Id.Should().NotBe(sourceId);
        clone.Status.Should().Be("PENDING");
        clone.AttemptCount.Should().Be(0);
        clone.EventType.Should().Be(source.EventType);
        clone.Payload.Should().Be(payload);
        clone.EndpointId.Should().Be(endpoint.Id);
        clone.OrganizationId.Should().Be(orgId);
        clone.NextAttemptAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        result.LastError.Should().BeNull();
    }

    [Test]
    public async Task Redeliver_Success_InsertsPendingClone()
    {
        await using var db = CreateDb();
        var repo = new OneRepository(db);
        var (orgId, endpoint, source) = await SeedAsync(db, terminal: "SUCCESS");

        var result = await new RedeliverWebhookDeliveryCommandHandler(repo)
            .Handle(new RedeliverWebhookDeliveryCommand(orgId, source.Id), CancellationToken.None);

        var original = await db.WebhookDeliveryOutboxes.IgnoreQueryFilters()
            .SingleAsync(d => d.Id == source.Id);
        original.Status.Should().Be("SUCCESS");

        var clone = await db.WebhookDeliveryOutboxes.IgnoreQueryFilters()
            .SingleAsync(d => d.Id == result.Id);
        clone.Status.Should().Be("PENDING");
        clone.AttemptCount.Should().Be(0);
        clone.Payload.Should().Be(source.Payload);
        clone.EndpointId.Should().Be(endpoint.Id);
        clone.Id.Should().NotBe(source.Id);
    }

    [Test]
    public async Task Redeliver_Pending_Throws()
    {
        await using var db = CreateDb();
        var repo = new OneRepository(db);
        var (orgId, _, source) = await SeedAsync(db, terminal: "PENDING");

        var act = () => new RedeliverWebhookDeliveryCommandHandler(repo)
            .Handle(new RedeliverWebhookDeliveryCommand(orgId, source.Id), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*pending*");

        (await db.WebhookDeliveryOutboxes.IgnoreQueryFilters().CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task Redeliver_Missing_ThrowsNotFound()
    {
        await using var db = CreateDb();
        var repo = new OneRepository(db);
        var (orgId, _, _) = await SeedAsync(db, terminal: "FAILED");

        var act = () => new RedeliverWebhookDeliveryCommandHandler(repo)
            .Handle(new RedeliverWebhookDeliveryCommand(orgId, Guid.CreateVersion7()), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*not found*");
    }

    [Test]
    public async Task Redeliver_WrongWorkspace_ThrowsNotFound()
    {
        await using var db = CreateDb();
        var repo = new OneRepository(db);
        var (_, _, source) = await SeedAsync(db, terminal: "FAILED");
        var otherOrg = Guid.CreateVersion7();

        var act = () => new RedeliverWebhookDeliveryCommandHandler(repo)
            .Handle(new RedeliverWebhookDeliveryCommand(otherOrg, source.Id), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*not found*");

        (await db.WebhookDeliveryOutboxes.IgnoreQueryFilters().CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task Redeliver_InactiveEndpoint_Throws()
    {
        await using var db = CreateDb();
        var repo = new OneRepository(db);
        var (orgId, endpoint, source) = await SeedAsync(db, terminal: "FAILED");
        endpoint.Disable();
        await db.SaveChangesAsync();

        var act = () => new RedeliverWebhookDeliveryCommandHandler(repo)
            .Handle(new RedeliverWebhookDeliveryCommand(orgId, source.Id), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*inactive*");

        (await db.WebhookDeliveryOutboxes.IgnoreQueryFilters().CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task Redeliver_Clone_IsClaimable()
    {
        await using var db = CreateDb();
        var repo = new OneRepository(db);
        var (orgId, _, source) = await SeedAsync(db, terminal: "FAILED");

        var result = await new RedeliverWebhookDeliveryCommandHandler(repo)
            .Handle(new RedeliverWebhookDeliveryCommand(orgId, source.Id), CancellationToken.None);

        var claimed = await OutboundWebhookDispatcherJob.ClaimPendingDeliveriesAsync(
            db, DateTime.UtcNow.AddMinutes(2), CancellationToken.None);

        claimed.Should().ContainSingle(d => d.Id == result.Id);
        claimed.Should().NotContain(d => d.Id == source.Id);
    }

    private static async Task<(Guid OrgId, TenantWebhookEndpoint Endpoint, WebhookDeliveryOutbox Delivery)> SeedAsync(
        OneDbContext db,
        string terminal)
    {
        var orgId = Guid.CreateVersion7();
        var endpoint = new TenantWebhookEndpoint(
            orgId,
            "https://aura.example/hooks",
            "whsec_test_redeliver");
        var payload =
            """{"id":"evt_redeliver","event_type":"payment.completed","created_at":"2026-01-01T00:00:00Z","data":{}}""";
        var delivery = new WebhookDeliveryOutbox(orgId, endpoint.Id, "payment.completed", payload);

        switch (terminal)
        {
            case "FAILED":
                delivery.RecordPermanentFailure("HTTP 401 Unauthorized");
                break;
            case "SUCCESS":
                delivery.RecordSuccess();
                break;
        }

        db.TenantWebhookEndpoints.Add(endpoint);
        db.WebhookDeliveryOutboxes.Add(delivery);
        await db.SaveChangesAsync();
        return (orgId, endpoint, delivery);
    }

    private static OneDbContext CreateDb()
        => new(
            InMemoryDb.CreateOptions<OneDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());
}
