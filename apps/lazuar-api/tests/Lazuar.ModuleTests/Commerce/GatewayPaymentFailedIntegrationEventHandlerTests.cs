using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Commerce.Contracts.Events;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.Entities;
using Modules.Commerce.Infrastructure;
using Modules.Commerce.Infrastructure.EventHandlers;
using Modules.CRM.Contracts;
using Modules.Payments.Contracts.Events;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class GatewayPaymentFailedIntegrationEventHandlerTests
{
    private CommerceDbContext _db = null!;
    private GatewayPaymentFailedIntegrationEventHandler _handler = null!;
    private IEventBus _eventBus = null!;
    private Guid _orgId;
    private Guid _productId;

    [SetUp]
    public void SetUp()
    {
        _orgId = Guid.CreateVersion7();
        _productId = Guid.CreateVersion7();

        _db = new CommerceDbContext(
            InMemoryDb.CreateOptions<CommerceDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());

        _eventBus = Substitute.For<IEventBus>();
        _handler = new GatewayPaymentFailedIntegrationEventHandler(
            _db,
            _eventBus,
            Substitute.For<ICrmQueryService>(),
            Substitute.For<ILogger<GatewayPaymentFailedIntegrationEventHandler>>());
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
    }

    [Test]
    public async Task HandleAsync_ActiveSubscription_MarksPastDue_AndAssignsMatchingCampaign()
    {
        var clientId = Guid.CreateVersion7();
        var sub = new Subscription(_orgId, clientId, _productId);
        sub.Activate(DateTime.UtcNow.AddMonths(1), DateTime.UtcNow.AddMonths(1));
        sub.StoreVaultedToken("cus_test", "pm_test");

        var campaign = new DunningCampaign(
            _orgId,
            "Default online",
            finalAction: "SUSPEND",
            gracePeriodDays: 7,
            priorityOrder: 10);

        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        var @event = new GatewayPaymentFailedIntegrationEvent(
            OrganizationId: _orgId,
            GatewayTransactionId: "pi_fail_1",
            Metadata: new Dictionary<string, string>
            {
                ["type"] = "commerce_subscription",
                ["subscription_id"] = sub.Id.ToString(),
                ["tenant_id"] = _orgId.ToString(),
                ["failure_reason"] = "charge_declined",
                ["gateway_name"] = "STRIPE"
            });

        await _handler.HandleAsync(@event);

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters().FirstAsync(s => s.Id == sub.Id);
        reloaded.Status.Should().Be("PAST_DUE");
        reloaded.CurrentDunningCampaignId.Should().Be(campaign.Id);

        await _eventBus.Received(1).PublishAsync(Arg.Is<OutboundWebhookRequestedIntegrationEvent>(e =>
            e.EventType == "subscription.past_due"
            && e.TargetUrl == null
            && e.OrganizationId == _orgId
            && e.Payload.GetProperty("subscription_id").GetString() == sub.Id.ToString()
            && e.Payload.GetProperty("status").GetString() == "PAST_DUE"));
    }

    [Test]
    public async Task HandleAsync_MarksPendingChargeAttemptFailed_ByChargeAttemptId()
    {
        var sub = new Subscription(_orgId, Guid.CreateVersion7(), _productId);
        sub.Activate(DateTime.UtcNow.AddMonths(1), DateTime.UtcNow.AddMonths(1));
        sub.StoreVaultedToken("cus_x", "pm_x");

        var target = DateTime.UtcNow.Date;
        var attempt1 = new ChargeAttemptLog(sub.Id, target, 1, ChargeAttemptLog.SourceBilling);
        var attempt2 = new ChargeAttemptLog(sub.Id, target, 2, ChargeAttemptLog.SourceDunning);

        var campaign = new DunningCampaign(_orgId, "Camp", "NONE", 7, priorityOrder: 1);

        _db.Subscriptions.Add(sub);
        _db.ChargeAttemptLogs.AddRange(attempt1, attempt2);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        var @event = new GatewayPaymentFailedIntegrationEvent(
            OrganizationId: _orgId,
            GatewayTransactionId: "off_session:" + sub.Id,
            Metadata: new Dictionary<string, string>
            {
                ["subscription_id"] = sub.Id.ToString(),
                ["charge_attempt_id"] = attempt2.Id.ToString(),
                ["failure_reason"] = "charge_declined",
                ["gateway_name"] = "STRIPE",
                ["gateway_response_code"] = "card_declined"
            });

        await _handler.HandleAsync(@event);

        var failed = await _db.ChargeAttemptLogs.FirstAsync(l => l.Id == attempt2.Id);
        failed.Status.Should().Be(ChargeAttemptLog.StatusFailed);
        failed.FailureReason.Should().Be("charge_declined");
        failed.GatewayName.Should().Be("STRIPE");
        failed.GatewayResponseCode.Should().Be("card_declined");
        failed.CompletedAt.Should().NotBeNull();

        var stillPending = await _db.ChargeAttemptLogs.FirstAsync(l => l.Id == attempt1.Id);
        stillPending.Status.Should().Be(ChargeAttemptLog.StatusPending);

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters().FirstAsync(s => s.Id == sub.Id);
        reloaded.Status.Should().Be("PAST_DUE");
    }

    [Test]
    public async Task HandleAsync_AlreadyPastDue_DoesNotReassignWhenCampaignPresent()
    {
        var existingCampaignId = Guid.CreateVersion7();
        var sub = new Subscription(_orgId, Guid.CreateVersion7(), _productId);
        sub.Activate(DateTime.UtcNow, DateTime.UtcNow);
        sub.MarkAsPastDue();
        sub.AssignDunningCampaign(existingCampaignId);

        var other = new DunningCampaign(_orgId, "Other", "NONE", 7, priorityOrder: 99);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(other);
        await _db.SaveChangesAsync();

        await _handler.HandleAsync(new GatewayPaymentFailedIntegrationEvent(
            _orgId,
            "pi_2",
            new Dictionary<string, string> { ["subscription_id"] = sub.Id.ToString() }));

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters().FirstAsync(s => s.Id == sub.Id);
        reloaded.Status.Should().Be("PAST_DUE");
        reloaded.CurrentDunningCampaignId.Should().Be(existingCampaignId);

        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<OutboundWebhookRequestedIntegrationEvent>());
    }

    [Test]
    public async Task HandleAsync_CanceledSubscription_SkipsPastDueButCanFailAttempt()
    {
        var sub = new Subscription(_orgId, Guid.CreateVersion7(), _productId);
        sub.Activate(DateTime.UtcNow, DateTime.UtcNow);
        sub.Cancel();

        var attempt = new ChargeAttemptLog(sub.Id, DateTime.UtcNow.Date, 1, ChargeAttemptLog.SourceBilling);
        _db.Subscriptions.Add(sub);
        _db.ChargeAttemptLogs.Add(attempt);
        await _db.SaveChangesAsync();

        await _handler.HandleAsync(new GatewayPaymentFailedIntegrationEvent(
            _orgId,
            "pi_canceled",
            new Dictionary<string, string>
            {
                ["subscription_id"] = sub.Id.ToString(),
                ["charge_attempt_id"] = attempt.Id.ToString(),
                ["failure_reason"] = "n/a"
            }));

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters().FirstAsync(s => s.Id == sub.Id);
        reloaded.Status.Should().Be("CANCELED");
        reloaded.CurrentDunningCampaignId.Should().BeNull();

        var failed = await _db.ChargeAttemptLogs.FirstAsync(l => l.Id == attempt.Id);
        failed.Status.Should().Be(ChargeAttemptLog.StatusFailed);
    }

    [Test]
    public async Task HandleAsync_MissingSubscriptionId_IsNoOp()
    {
        var sub = new Subscription(_orgId, Guid.CreateVersion7(), _productId);
        sub.Activate(DateTime.UtcNow, DateTime.UtcNow);
        _db.Subscriptions.Add(sub);
        await _db.SaveChangesAsync();

        await _handler.HandleAsync(new GatewayPaymentFailedIntegrationEvent(
            _orgId,
            "pi_orphan",
            new Dictionary<string, string> { ["type"] = "commerce_subscription" }));

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters().FirstAsync(s => s.Id == sub.Id);
        reloaded.Status.Should().Be("ACTIVE");
    }

    [Test]
    public async Task HandleAsync_PrefersHigherPriorityCampaignForOrg()
    {
        var sub = new Subscription(_orgId, Guid.CreateVersion7(), _productId);
        sub.Activate(DateTime.UtcNow, DateTime.UtcNow);
        sub.StoreVaultedToken("cus", "tok");

        var low = new DunningCampaign(_orgId, "Low", "NONE", 7, priorityOrder: 1);
        var high = new DunningCampaign(_orgId, "High", "NONE", 7, priorityOrder: 50);
        var otherOrg = new DunningCampaign(Guid.CreateVersion7(), "Other org", "NONE", 7, priorityOrder: 100);

        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.AddRange(low, high, otherOrg);
        await _db.SaveChangesAsync();

        await _handler.HandleAsync(new GatewayPaymentFailedIntegrationEvent(
            _orgId,
            "pi_prio",
            new Dictionary<string, string> { ["subscription_id"] = sub.Id.ToString() }));

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters().FirstAsync(s => s.Id == sub.Id);
        reloaded.CurrentDunningCampaignId.Should().Be(high.Id);
    }

    [Test]
    public async Task HandleAsync_ResolvesSubscriptionIdFromReceiptFallback()
    {
        var sub = new Subscription(_orgId, Guid.CreateVersion7(), _productId);
        sub.Activate(DateTime.UtcNow, DateTime.UtcNow);
        sub.StoreVaultedToken("c", "t");
        var campaign = new DunningCampaign(_orgId, "C", "NONE", 7, 1);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _handler.HandleAsync(new GatewayPaymentFailedIntegrationEvent(
            _orgId,
            "pi_receipt",
            new Dictionary<string, string> { ["receipt"] = sub.Id.ToString() }));

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters().FirstAsync(s => s.Id == sub.Id);
        reloaded.Status.Should().Be("PAST_DUE");
        reloaded.CurrentDunningCampaignId.Should().Be(campaign.Id);
    }
}
