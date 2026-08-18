using System;
using System.Threading.Tasks;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.TestSupport;
using Microsoft.EntityFrameworkCore;
using Modules.Communications.Contracts;
using Modules.Communications.Domain.Aggregates;
using Modules.Communications.Infrastructure;
using Modules.Communications.Infrastructure.Services;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Communications;

[TestFixture]
public class SuppressionLaneTests
{
    [Test]
    public async Task Unsubscribe_Blocks_Marketing_Not_Transactional()
    {
        var org = Guid.CreateVersion7();
        await using var db = CreateDb();
        db.SuppressionEntries.Add(new SuppressionEntry(org, "user@example.com", "UNSUBSCRIBE"));
        await db.SaveChangesAsync();

        var svc = new SuppressionService(db);
        (await svc.IsSuppressedAsync(org, "user@example.com", SuppressionLane.Transactional)).Should().BeFalse();
        (await svc.IsSuppressedAsync(org, "user@example.com", SuppressionLane.Marketing)).Should().BeTrue();
    }

    [Test]
    public async Task Unsubscribe_Then_Bounce_Upgrades_And_Blocks_Transactional()
    {
        var org = Guid.CreateVersion7();
        await using var db = CreateDb();
        var svc = new SuppressionService(db);

        await svc.SuppressAsync(org, "user@example.com", "UNSUBSCRIBE", "list_unsubscribe");
        (await svc.IsSuppressedAsync(org, "user@example.com", SuppressionLane.Transactional)).Should().BeFalse();

        await svc.SuppressAsync(org, "user@example.com", "BOUNCE", "resend_webhook");
        (await svc.IsSuppressedAsync(org, "user@example.com", SuppressionLane.Transactional)).Should().BeTrue();
        (await svc.IsSuppressedAsync(org, "user@example.com", SuppressionLane.Marketing)).Should().BeTrue();

        var row = await db.SuppressionEntries.IgnoreQueryFilters().SingleAsync();
        row.Reason.Should().Be("BOUNCE");
        row.Source.Should().Be("resend_webhook");
    }

    [Test]
    public async Task Bounce_Does_Not_Downgrade_To_Unsubscribe()
    {
        var org = Guid.CreateVersion7();
        await using var db = CreateDb();
        var svc = new SuppressionService(db);
        await svc.SuppressAsync(org, "user@example.com", "BOUNCE", "resend_webhook");
        await svc.SuppressAsync(org, "user@example.com", "UNSUBSCRIBE", "list_unsubscribe");

        var row = await db.SuppressionEntries.IgnoreQueryFilters().SingleAsync();
        row.Reason.Should().Be("BOUNCE");
        (await svc.IsSuppressedAsync(org, "user@example.com", SuppressionLane.Transactional)).Should().BeTrue();
    }

    [Test]
    public async Task Bounce_Blocks_Both()
    {
        var org = Guid.CreateVersion7();
        await using var db = CreateDb();
        db.SuppressionEntries.Add(new SuppressionEntry(org, "user@example.com", "BOUNCE"));
        await db.SaveChangesAsync();

        var svc = new SuppressionService(db);
        (await svc.IsSuppressedAsync(org, "user@example.com", SuppressionLane.Transactional)).Should().BeTrue();
        (await svc.IsSuppressedAsync(org, "user@example.com", SuppressionLane.Marketing)).Should().BeTrue();
    }

    private static CommunicationsDbContext CreateDb()
    {
        return new CommunicationsDbContext(
            InMemoryDb.CreateOptions<CommunicationsDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());
    }
}
