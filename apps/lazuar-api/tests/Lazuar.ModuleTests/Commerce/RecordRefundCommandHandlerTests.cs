using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.TestSupport;
using Microsoft.EntityFrameworkCore;
using Modules.Commerce.Application;
using Modules.Commerce.Application.Commands;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Domain.Entities;
using Modules.Commerce.Infrastructure;
using Modules.Commerce.Infrastructure.Repositories;
using Modules.Payments.Contracts.Events;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class RecordRefundCommandHandlerTests
{
    [Test]
    public async Task Handle_Publishes_And_Persists_Outbox()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var log = ConfirmedLog(orgId, 100m, "STRIPE", "pi_full");
        db.TransactionLogs.Add(log);
        await db.SaveChangesAsync();

        var status = await CreateHandler(db).Handle(
            new RecordRefundCommand(orgId, log.Id), CancellationToken.None);

        status.Should().Be("refund_requested");
        var stored = await db.TransactionLogs.IgnoreQueryFilters().SingleAsync();
        stored.Status.Should().Be(CommerceTransactionLog.StatusRefundPending);

        var row = await db.OutboxMessages.SingleAsync();
        row.Type.Should().Contain(nameof(GatewayRefundRequestedIntegrationEvent));
        row.ProcessedAt.Should().BeNull();

        using var doc = JsonDocument.Parse(row.Data);
        doc.RootElement.GetProperty("Amount").GetDecimal().Should().Be(100m);
        doc.RootElement.GetProperty("GatewayTransactionId").GetString().Should().Be("pi_full");
        doc.RootElement.GetProperty("GatewayName").GetString().Should().Be("STRIPE");
        doc.RootElement.GetProperty("IsFullRefund").GetBoolean().Should().BeTrue();
    }

    [Test]
    public async Task Handle_DoesNotDefaultStripe_WhenGatewayMissing()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var log = ConfirmedLog(orgId, 50m, gatewayName: null, "pi_orphan");
        db.TransactionLogs.Add(log);
        await db.SaveChangesAsync();

        var act = () => CreateHandler(db).Handle(new RecordRefundCommand(orgId, log.Id), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<RefundRejectedException>();
        ex.Which.Code.Should().Be("GATEWAY_REQUIRED");
        (await db.OutboxMessages.CountAsync()).Should().Be(0);
        (await db.TransactionLogs.IgnoreQueryFilters().SingleAsync()).Status.Should().Be(CommerceTransactionLog.StatusConfirmed);
    }

    [Test]
    public async Task Handle_UsesLogGatewayName()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var log = ConfirmedLog(orgId, 80m, "CHIP", "purch_1");
        db.TransactionLogs.Add(log);
        await db.SaveChangesAsync();

        await CreateHandler(db).Handle(new RecordRefundCommand(orgId, log.Id), CancellationToken.None);

        using var doc = JsonDocument.Parse((await db.OutboxMessages.SingleAsync()).Data);
        doc.RootElement.GetProperty("GatewayName").GetString().Should().Be("CHIP");
    }

    [Test]
    public async Task Handle_Rejects_AlreadyRefunded()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var log = ConfirmedLog(orgId, 40m, "STRIPE", "pi_done");
        log.ApplyRefund(40m);
        db.TransactionLogs.Add(log);
        await db.SaveChangesAsync();

        var act = () => CreateHandler(db).Handle(new RecordRefundCommand(orgId, log.Id), CancellationToken.None);

        (await act.Should().ThrowAsync<RefundRejectedException>()).Which.Code.Should().Be("ALREADY_REFUNDED");
        (await db.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Handle_Rejects_Pending()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var log = ConfirmedLog(orgId, 40m, "STRIPE", "pi_pend");
        log.MarkRefundPending();
        db.TransactionLogs.Add(log);
        await db.SaveChangesAsync();

        var act = () => CreateHandler(db).Handle(new RecordRefundCommand(orgId, log.Id), CancellationToken.None);

        (await act.Should().ThrowAsync<RefundRejectedException>()).Which.Code.Should().Be("REFUND_PENDING");
        (await db.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Handle_Rejects_Billplz_WithoutMark()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var log = ConfirmedLog(orgId, 25m, "BILLPLZ", "bill_1");
        db.TransactionLogs.Add(log);
        await db.SaveChangesAsync();

        var act = () => CreateHandler(db).Handle(new RecordRefundCommand(orgId, log.Id), CancellationToken.None);

        (await act.Should().ThrowAsync<RefundRejectedException>()).Which.Code.Should().Be("MARK_REFUNDED_REQUIRED");
        (await db.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Handle_MarkRefunded_Billplz_PublishesCompleted_NotRequested()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var log = ConfirmedLog(orgId, 25m, "BILLPLZ", "bill_mark");
        db.TransactionLogs.Add(log);
        await db.SaveChangesAsync();

        var status = await CreateHandler(db).Handle(
            new RecordRefundCommand(orgId, log.Id, MarkRefunded: true), CancellationToken.None);

        status.Should().Be("refunded");
        var stored = await db.TransactionLogs.IgnoreQueryFilters().SingleAsync();
        stored.Status.Should().Be(CommerceTransactionLog.StatusRefunded);
        stored.RefundedAmount.Should().Be(25m);

        var row = await db.OutboxMessages.SingleAsync();
        row.Type.Should().Contain(nameof(GatewayRefundCompletedIntegrationEvent));
        using var doc = JsonDocument.Parse(row.Data);
        doc.RootElement.GetProperty("IsFullRefund").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("RefundedAmount").GetDecimal().Should().Be(25m);
    }

    [Test]
    public async Task Handle_Rejects_Offline_WithoutMark()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var log = ConfirmedLog(orgId, 15m, "OFFLINE", "OFFLINE-abc");
        db.TransactionLogs.Add(log);
        await db.SaveChangesAsync();

        var act = () => CreateHandler(db).Handle(new RecordRefundCommand(orgId, log.Id), CancellationToken.None);

        (await act.Should().ThrowAsync<RefundRejectedException>()).Which.Code.Should().Be("MARK_REFUNDED_REQUIRED");
        (await db.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Handle_Persists_Reason()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var log = ConfirmedLog(orgId, 10m, "STRIPE", "pi_reason");
        db.TransactionLogs.Add(log);
        await db.SaveChangesAsync();

        await CreateHandler(db).Handle(
            new RecordRefundCommand(orgId, log.Id, Reason: "Customer requested cancellation"),
            CancellationToken.None);

        (await db.TransactionLogs.IgnoreQueryFilters().SingleAsync()).RefundReason.Should().Be("Customer requested cancellation");
    }

    [Test]
    public async Task Handle_Partial_SetsPending_AmountOnEvent()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var log = ConfirmedLog(orgId, 100m, "STRIPE", "pi_partial");
        db.TransactionLogs.Add(log);
        await db.SaveChangesAsync();

        await CreateHandler(db).Handle(
            new RecordRefundCommand(orgId, log.Id, Amount: 40m), CancellationToken.None);

        var stored = await db.TransactionLogs.IgnoreQueryFilters().SingleAsync();
        stored.Status.Should().Be(CommerceTransactionLog.StatusRefundPending);
        stored.RefundedAmount.Should().Be(0m);

        using var doc = JsonDocument.Parse((await db.OutboxMessages.SingleAsync()).Data);
        doc.RootElement.GetProperty("Amount").GetDecimal().Should().Be(40m);
        doc.RootElement.GetProperty("IsFullRefund").GetBoolean().Should().BeFalse();
    }

    [Test]
    public async Task Handle_OmittedAmount_AfterPartial_UsesRemaining()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var log = ConfirmedLog(orgId, 100m, "STRIPE", "pi_rest");
        log.ApplyRefund(40m);
        db.TransactionLogs.Add(log);
        await db.SaveChangesAsync();

        await CreateHandler(db).Handle(new RecordRefundCommand(orgId, log.Id), CancellationToken.None);

        using var doc = JsonDocument.Parse((await db.OutboxMessages.SingleAsync()).Data);
        doc.RootElement.GetProperty("Amount").GetDecimal().Should().Be(60m);
        doc.RootElement.GetProperty("IsFullRefund").GetBoolean().Should().BeTrue();
    }

    [Test]
    public async Task Handle_AmountGreaterThanRemaining_Throws()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var log = ConfirmedLog(orgId, 100m, "STRIPE", "pi_over");
        log.ApplyRefund(40m);
        db.TransactionLogs.Add(log);
        await db.SaveChangesAsync();

        var act = () => CreateHandler(db).Handle(
            new RecordRefundCommand(orgId, log.Id, Amount: 70m), CancellationToken.None);

        (await act.Should().ThrowAsync<RefundRejectedException>()).Which.Code.Should().Be("AMOUNT_EXCEEDS_REMAINING");
        (await db.OutboxMessages.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Handle_FromPartiallyRefunded_Allowed()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var log = ConfirmedLog(orgId, 100m, "STRIPE", "pi_again");
        log.ApplyRefund(40m);
        db.TransactionLogs.Add(log);
        await db.SaveChangesAsync();

        var status = await CreateHandler(db).Handle(
            new RecordRefundCommand(orgId, log.Id, Amount: 20m), CancellationToken.None);

        status.Should().Be("refund_requested");
        (await db.TransactionLogs.IgnoreQueryFilters().SingleAsync()).Status.Should().Be(CommerceTransactionLog.StatusRefundPending);
    }

    [Test]
    public async Task Handle_FromRefunded_StillRejected()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var log = ConfirmedLog(orgId, 100m, "STRIPE", "pi_term");
        log.ApplyRefund(100m);
        db.TransactionLogs.Add(log);
        await db.SaveChangesAsync();

        var act = () => CreateHandler(db).Handle(
            new RecordRefundCommand(orgId, log.Id, Amount: 1m), CancellationToken.None);

        (await act.Should().ThrowAsync<RefundRejectedException>()).Which.Code.Should().Be("ALREADY_REFUNDED");
    }

    [Test]
    public async Task Handle_FromDisputed_MarkRefunded_PublishesCompleted()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var log = ConfirmedLog(orgId, 25m, "BILLPLZ", "bill_dispute");
        log.MarkDisputed();
        db.TransactionLogs.Add(log);
        await db.SaveChangesAsync();

        var status = await CreateHandler(db).Handle(
            new RecordRefundCommand(orgId, log.Id, MarkRefunded: true), CancellationToken.None);

        status.Should().Be("refunded");
        var stored = await db.TransactionLogs.IgnoreQueryFilters().SingleAsync();
        stored.Status.Should().Be(CommerceTransactionLog.StatusRefunded);
        stored.RefundedAmount.Should().Be(25m);

        var row = await db.OutboxMessages.SingleAsync();
        row.Type.Should().Contain(nameof(GatewayRefundCompletedIntegrationEvent));
        using var doc = JsonDocument.Parse(row.Data);
        doc.RootElement.GetProperty("RefundedAmount").GetDecimal().Should().Be(25m);
        doc.RootElement.GetProperty("IsFullRefund").GetBoolean().Should().BeTrue();
    }

    [Test]
    public async Task Handle_MarkRefunded_Partial_Billplz()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var log = ConfirmedLog(orgId, 100m, "BILLPLZ", "bill_slice");
        db.TransactionLogs.Add(log);
        await db.SaveChangesAsync();

        await CreateHandler(db).Handle(
            new RecordRefundCommand(orgId, log.Id, Amount: 20m, MarkRefunded: true),
            CancellationToken.None);

        var stored = await db.TransactionLogs.IgnoreQueryFilters().SingleAsync();
        stored.Status.Should().Be(CommerceTransactionLog.StatusPartiallyRefunded);
        stored.RefundedAmount.Should().Be(20m);

        using var doc = JsonDocument.Parse((await db.OutboxMessages.SingleAsync()).Data);
        doc.RootElement.GetProperty("IsFullRefund").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("RefundedAmount").GetDecimal().Should().Be(20m);
    }

    private static CommerceTransactionLog ConfirmedLog(
        Guid orgId, decimal amount, string? gatewayName, string externalRef) =>
        new(
            orgId,
            amount,
            0m,
            "MYR",
            CommerceTransactionLog.StatusConfirmed,
            "Alice",
            "a@b.com",
            "Plan",
            "SYSTEM",
            externalRef,
            gatewayName);

    private static CommerceDbContext CreateDb() =>
        new(
            InMemoryDb.CreateOptions<CommerceDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());

    private static RecordRefundCommandHandler CreateHandler(CommerceDbContext db) =>
        new(new CommerceRepository(db), new OutboxEventBus<CommerceDbContext>(db));
}
