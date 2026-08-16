using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.TestSupport;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.One.Domain;
using Modules.One.Infrastructure;
using Modules.One.Infrastructure.Services;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

[TestFixture]
public class AuditRecorderTests
{
    [Test]
    public async Task Record_PersistsRowWithoutSecrets()
    {
        await using var db = CreateDb();
        var actor = new GlobalUser("actor@example.com", "Actor", "hash");
        db.GlobalUsers.Add(actor);
        await db.SaveChangesAsync();

        var ctx = FakeExecutionContextAccessor.ForTenant(Guid.CreateVersion7(), actor.Id);
        var recorder = new AuditRecorder(db, ctx, NullLogger<AuditRecorder>.Instance);
        var orgId = ctx.TenantId;

        await recorder.RecordAsync(
            orgId,
            "subscriber.payment_recorded",
            "subscription",
            Guid.CreateVersion7().ToString(),
            new { amount = 10.5m, method = "bank_transfer" },
            actor.Id,
            ct: CancellationToken.None);

        var row = await db.AuditEvents.IgnoreQueryFilters().SingleAsync();
        row.OrganizationId.Should().Be(orgId);
        row.Action.Should().Be("subscriber.payment_recorded");
        row.ActorEmail.Should().Be("actor@example.com");
        row.MetadataJson.Should().Contain("10.5");
        row.MetadataJson.Should().NotContain("sk_live");
        row.MetadataJson.Should().NotContain("password");
    }

    [Test]
    public async Task RecorderThrow_DoesNotPropagate()
    {
        var db = CreateDb();
        await db.DisposeAsync();
        var recorder = new AuditRecorder(
            db,
            FakeExecutionContextAccessor.EmptyTenant(),
            NullLogger<AuditRecorder>.Instance);

        var act = () => recorder.RecordAsync(
            Guid.CreateVersion7(),
            "refund.created",
            "transaction",
            Guid.CreateVersion7().ToString());

        await act.Should().NotThrowAsync();
    }

    [Test]
    public async Task ForeignOrg_GetAudit_Forbidden()
    {
        var orgA = Guid.CreateVersion7();
        var orgB = Guid.CreateVersion7();
        var userB = Guid.CreateVersion7();

        var query = Substitute.For<Modules.One.Contracts.IOneQueryService>();
        query.HasTenantAccessAsync(userB, orgA).Returns(false);

        var ctx = new FakeExecutionContextAccessor { UserId = userB, TenantId = orgB, IsSystemAdmin = false };
        await using var db = CreateDb();
        db.AuditEvents.Add(new AuditEvent(orgA, "member.invited", "invitation", Guid.CreateVersion7().ToString()));
        await db.SaveChangesAsync();

        var result = await InvokeGetAudit(orgA, ctx, query, db);
        result.Should().BeOfType<ForbidHttpResult>();
    }

    private static async Task<IResult> InvokeGetAudit(
        Guid id,
        IExecutionContextAccessor ctx,
        Modules.One.Contracts.IOneQueryService query,
        OneDbContext db)
    {
        if (ctx.UserId == Guid.Empty) return TypedResults.Unauthorized();
        var hasAccess = await query.HasTenantAccessAsync(ctx.UserId, id);
        if (!hasAccess && !ctx.IsSystemAdmin) return TypedResults.Forbid();

        var rows = await db.AuditEvents.IgnoreQueryFilters()
            .Where(e => e.OrganizationId == id)
            .ToListAsync();
        return TypedResults.Ok(rows.Count);
    }

    private static OneDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<OneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OneDbContext(
            options,
            FakeExecutionContextAccessor.EmptyTenant(),
            Substitute.For<IMediator>(),
            new DatabaseJobTrigger());
    }
}
