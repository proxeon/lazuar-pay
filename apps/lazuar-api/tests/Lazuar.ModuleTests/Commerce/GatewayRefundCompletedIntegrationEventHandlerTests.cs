using System;
using System.Threading.Tasks;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.TestSupport;
using Microsoft.EntityFrameworkCore;
using Modules.Commerce.Domain.Entities;
using Modules.Commerce.Infrastructure;
using Modules.Commerce.Infrastructure.EventHandlers;
using Modules.Payments.Contracts.Events;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class GatewayRefundCompletedIntegrationEventHandlerTests
{
    [Test]
    public async Task Completed_MatchesExternalReference_RefundsFull()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var log = Confirmed(orgId, 100m, "pi_full");
        log.MarkRefundPending();
        db.TransactionLogs.Add(log);
        await db.SaveChangesAsync();

        await new GatewayRefundCompletedIntegrationEventHandler(db).HandleAsync(
            Completed(orgId, log.Id, "pi_full", 100m));

        var stored = await db.TransactionLogs.IgnoreQueryFilters().SingleAsync();
        stored.Status.Should().Be(CommerceTransactionLog.StatusRefunded);
        stored.RefundedAmount.Should().Be(100m);
    }

    [Test]
    public async Task Completed_Partial_SetsPartiallyRefunded()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var log = Confirmed(orgId, 100m, "pi_slice");
        log.MarkRefundPending();
        db.TransactionLogs.Add(log);
        await db.SaveChangesAsync();

        await new GatewayRefundCompletedIntegrationEventHandler(db).HandleAsync(
            Completed(orgId, log.Id, "pi_slice", 40m));

        var stored = await db.TransactionLogs.IgnoreQueryFilters().SingleAsync();
        stored.Status.Should().Be(CommerceTransactionLog.StatusPartiallyRefunded);
        stored.RefundedAmount.Should().Be(40m);
    }

    [Test]
    public async Task Completed_SecondSlice_ReachesRefunded()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var log = Confirmed(orgId, 100m, "pi_two");
        log.ApplyRefund(40m);
        log.MarkRefundPending();
        db.TransactionLogs.Add(log);
        await db.SaveChangesAsync();

        await new GatewayRefundCompletedIntegrationEventHandler(db).HandleAsync(
            Completed(orgId, log.Id, "pi_two", 60m));

        var stored = await db.TransactionLogs.IgnoreQueryFilters().SingleAsync();
        stored.Status.Should().Be(CommerceTransactionLog.StatusRefunded);
        stored.RefundedAmount.Should().Be(100m);
    }

    [Test]
    public async Task Completed_WhenNotPending_DoesNotDoubleAdd()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var log = Confirmed(orgId, 100m, "pi_once");
        log.ApplyRefund(40m);
        db.TransactionLogs.Add(log);
        await db.SaveChangesAsync();

        await new GatewayRefundCompletedIntegrationEventHandler(db).HandleAsync(
            Completed(orgId, log.Id, "pi_once", 40m));

        var stored = await db.TransactionLogs.IgnoreQueryFilters().SingleAsync();
        stored.Status.Should().Be(CommerceTransactionLog.StatusPartiallyRefunded);
        stored.RefundedAmount.Should().Be(40m);
    }

    [Test]
    public async Task InboundConfirmed_AppliesWithoutPending()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var log = Confirmed(orgId, 100m, "pi_dash");
        db.TransactionLogs.Add(log);
        await db.SaveChangesAsync();

        await new GatewayRefundCompletedIntegrationEventHandler(db).HandleAsync(
            Completed(orgId, Guid.Empty, "pi_dash", 40m, refundId: "re_1"));

        var stored = await db.TransactionLogs.IgnoreQueryFilters().SingleAsync();
        stored.Status.Should().Be(CommerceTransactionLog.StatusPartiallyRefunded);
        stored.RefundedAmount.Should().Be(40m);
    }

    [Test]
    public async Task Failed_OnPending_SetsRefundFailed()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var log = Confirmed(orgId, 50m, "pi_fail");
        log.MarkRefundPending();
        db.TransactionLogs.Add(log);
        await db.SaveChangesAsync();

        await new GatewayRefundFailedIntegrationEventHandler(db).HandleAsync(
            new GatewayRefundFailedIntegrationEvent(orgId, Guid.Empty, log.Id, "adapter failed"));

        var stored = await db.TransactionLogs.IgnoreQueryFilters().SingleAsync();
        stored.Status.Should().Be(CommerceTransactionLog.StatusRefundFailed);
        stored.RefundedAmount.Should().Be(0m);
    }

    [Test]
    public async Task Failed_UnknownId_DoesNotThrow()
    {
        await using var db = CreateDb();
        var act = () => new GatewayRefundFailedIntegrationEventHandler(db).HandleAsync(
            new GatewayRefundFailedIntegrationEvent(Guid.CreateVersion7(), Guid.Empty, Guid.CreateVersion7(), "missing"));

        await act.Should().NotThrowAsync();
    }

    private static CommerceTransactionLog Confirmed(Guid orgId, decimal amount, string ext) =>
        new(orgId, amount, 0m, "MYR", CommerceTransactionLog.StatusConfirmed, "A", "a@b.com", "Plan", "SYSTEM", ext, "STRIPE");

    private static GatewayRefundCompletedIntegrationEvent Completed(
        Guid orgId, Guid paymentRecordId, string gatewayTx, decimal amount, string? refundId = null) =>
        new(orgId, Guid.Empty, paymentRecordId, gatewayTx, amount, "MYR", 0m, amount, RefundId: refundId);

    private static CommerceDbContext CreateDb() =>
        new(
            InMemoryDb.CreateOptions<CommerceDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());
}
