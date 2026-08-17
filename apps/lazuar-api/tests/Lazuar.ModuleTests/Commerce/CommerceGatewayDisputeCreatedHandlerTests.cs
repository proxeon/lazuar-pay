using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.Entities;
using Modules.Commerce.Infrastructure;
using Modules.Commerce.Infrastructure.EventHandlers;
using Modules.Payments.Contracts;
using Modules.Payments.Contracts.Events;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class CommerceGatewayDisputeCreatedHandlerTests
{
    private CommerceDbContext _db = null!;
    private CommerceGatewayDisputeCreatedHandler _handler = null!;
    private Guid _orgId;

    [SetUp]
    public void SetUp()
    {
        _orgId = Guid.CreateVersion7();
        _db = new CommerceDbContext(
            InMemoryDb.CreateOptions<CommerceDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());
        _handler = new CommerceGatewayDisputeCreatedHandler(
            _db,
            NullLogger<CommerceGatewayDisputeCreatedHandler>.Instance);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task Replay_SameGatewayTransactionId_PersistsOneRow_AndHealsHasOpenDispute()
    {
        var sub = ActiveSub();
        _db.Subscriptions.Add(sub);
        _db.Disputes.Add(new CommerceDispute(_orgId, "pi_dup", 50m, "MYR", sub.Id));
        await _db.SaveChangesAsync();
        sub.HasOpenDispute.Should().BeFalse();

        var evt = Dispute("pi_dup", subscriptionId: sub.Id);
        await _handler.HandleAsync(evt);
        await _handler.HandleAsync(evt);

        (await _db.Disputes.IgnoreQueryFilters().CountAsync()).Should().Be(1);
        var reloaded = await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == sub.Id);
        reloaded.Status.Should().Be("ACTIVE");
        reloaded.HasOpenDispute.Should().BeTrue();
        await AssertDidNotReceiveGatewayRefundCompleted();
    }

    [Test]
    public async Task UtilityType_NoOps()
    {
        await _handler.HandleAsync(Dispute(
            "pi_util",
            type: PlatformCheckoutTypes.UtilityCreditTopup));

        (await _db.Disputes.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        await AssertDidNotReceiveGatewayRefundCompleted();
    }

    [Test]
    public async Task PlatformSaasFee_NoOps()
    {
        await _handler.HandleAsync(Dispute(
            "pi_saas",
            type: PlatformCheckoutTypes.PlatformSaasFee));

        (await _db.Disputes.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        await AssertDidNotReceiveGatewayRefundCompleted();
    }

    [Test]
    public async Task Subscription_IsNotCanceled()
    {
        var sub = ActiveSub();
        var log = new CommerceTransactionLog(
            _orgId, 50m, 0m, "MYR", CommerceTransactionLog.StatusConfirmed,
            "Buyer", "buyer@example.com", "Plan", "STRIPE", "pi_sub", "STRIPE", sub.Id);
        _db.Subscriptions.Add(sub);
        _db.TransactionLogs.Add(log);
        await _db.SaveChangesAsync();

        await _handler.HandleAsync(Dispute("pi_sub", subscriptionId: sub.Id));

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == sub.Id);
        reloaded.Status.Should().NotBe("CANCELED");
        reloaded.Status.Should().Be("ACTIVE");
        reloaded.HasOpenDispute.Should().BeTrue();
        var dispute = await _db.Disputes.IgnoreQueryFilters().SingleAsync();
        dispute.SubscriptionId.Should().Be(sub.Id);
        dispute.Status.Should().Be(CommerceDispute.StatusOpen);
        (await _db.TransactionLogs.IgnoreQueryFilters().SingleAsync(l => l.Id == log.Id))
            .Status.Should().Be(CommerceTransactionLog.StatusDisputed);
        await AssertDidNotReceiveGatewayRefundCompleted();
    }

    [Test]
    public async Task RefundedLog_DisputeDoesNotOverwriteStatus()
    {
        var log = new CommerceTransactionLog(
            _orgId, 50m, 0m, "MYR", CommerceTransactionLog.StatusConfirmed,
            "Buyer", "buyer@example.com", "Plan", "STRIPE", "pi_ref", "STRIPE");
        log.ApplyRefund(50m);
        _db.TransactionLogs.Add(log);
        await _db.SaveChangesAsync();

        await _handler.HandleAsync(Dispute("pi_ref"));

        var stored = await _db.TransactionLogs.IgnoreQueryFilters().SingleAsync();
        stored.Status.Should().Be(CommerceTransactionLog.StatusRefunded);
        stored.RefundedAmount.Should().Be(50m);
        (await _db.Disputes.IgnoreQueryFilters().CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task NoMetadata_PersistsDispute_NoSubMutation()
    {
        var sub = ActiveSub();
        _db.Subscriptions.Add(sub);
        await _db.SaveChangesAsync();

        await _handler.HandleAsync(new GatewayDisputeCreatedIntegrationEvent(
            _orgId, "pi_none", 10m, "MYR", new Dictionary<string, string>()));

        (await _db.Disputes.IgnoreQueryFilters().CountAsync()).Should().Be(1);
        var reloaded = await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == sub.Id);
        reloaded.Status.Should().Be("ACTIVE");
        reloaded.HasOpenDispute.Should().BeFalse();
        await AssertDidNotReceiveGatewayRefundCompleted();
    }

    [Test]
    public async Task ClosedDispute_ClearsHasOpenDispute()
    {
        var sub = ActiveSub();
        sub.MarkHasOpenDispute();
        _db.Subscriptions.Add(sub);
        _db.Disputes.Add(new CommerceDispute(_orgId, "pi_closed", 50m, "MYR", sub.Id));
        await _db.SaveChangesAsync();

        var closer = new CommerceGatewayDisputeClosedHandler(
            _db, NullLogger<CommerceGatewayDisputeClosedHandler>.Instance);
        await closer.HandleAsync(new GatewayDisputeClosedIntegrationEvent(
            _orgId, "pi_closed", "won"));

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == sub.Id);
        reloaded.HasOpenDispute.Should().BeFalse();
        (await _db.Disputes.IgnoreQueryFilters().SingleAsync()).Status.Should().Be(CommerceDispute.StatusWon);
    }

    private async Task AssertDidNotReceiveGatewayRefundCompleted()
    {
        (await _db.OutboxMessages.CountAsync()).Should().Be(0);
    }

    private Subscription ActiveSub()
    {
        var sub = new Subscription(_orgId, Guid.CreateVersion7(), Guid.CreateVersion7());
        sub.Activate(DateTime.UtcNow.AddMonths(1), DateTime.UtcNow.AddMonths(1));
        return sub;
    }

    private GatewayDisputeCreatedIntegrationEvent Dispute(
        string gatewayTxId,
        Guid? subscriptionId = null,
        string? type = "commerce_subscription")
    {
        var meta = new Dictionary<string, string>();
        if (type != null) meta["type"] = type;
        if (subscriptionId.HasValue) meta["subscription_id"] = subscriptionId.Value.ToString();
        return new GatewayDisputeCreatedIntegrationEvent(_orgId, gatewayTxId, 50m, "MYR", meta);
    }
}
