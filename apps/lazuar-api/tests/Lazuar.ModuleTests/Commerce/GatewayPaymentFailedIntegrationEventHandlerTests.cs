using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Modules.Commerce.Contracts.Events;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.Entities;
using Modules.Commerce.Domain.ValueObjects;
using Modules.Commerce.Infrastructure;
using Modules.Commerce.Infrastructure.EventHandlers;
using Lazuar.ApiTypes;
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
        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(ci => new ClientProfileDto
        {
            Id = ci.Arg<Guid>().ToString(),
            Full_name = "Buyer",
            Email = "buyer@example.com"
        });
        _handler = new GatewayPaymentFailedIntegrationEventHandler(
            _db,
            _eventBus,
            crm,
            Substitute.For<ILogger<GatewayPaymentFailedIntegrationEventHandler>>(),
            new ConfigurationBuilder().AddInMemoryCollection().Build());
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
        var snapshot = reloaded.TryGetDunningCampaignSnapshot();
        snapshot.Should().NotBeNull();
        snapshot!.CampaignId.Should().Be(campaign.Id);
        snapshot.GracePeriodDays.Should().Be(7);
        snapshot.FinalAction.Should().Be("SUSPEND");

        await _eventBus.Received(1).PublishAsync(Arg.Is<OutboundWebhookRequestedIntegrationEvent>(e =>
            e.EventType == "subscription.past_due"
            && e.TargetUrl == null
            && e.OrganizationId == _orgId
            && e.Payload.GetProperty("subscription_id").GetString() == sub.Id.ToString()
            && e.Payload.GetProperty("status").GetString() == "PAST_DUE"));
    }

    [Test]
    public async Task HandleAsync_UpdatePaymentDecline_KeepsActive_DoesNotAssignCampaign()
    {
        var nextBilling = DateTime.UtcNow.AddDays(20);
        var sub = new Subscription(_orgId, Guid.CreateVersion7(), _productId);
        sub.Activate(DateTime.UtcNow.AddMonths(-1), nextBilling);
        sub.StoreVaultedToken("cus_live", "pm_live");

        var campaign = new DunningCampaign(_orgId, "Default online", "SUSPEND", 7, priorityOrder: 10);

        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        var @event = new GatewayPaymentFailedIntegrationEvent(
            OrganizationId: _orgId,
            GatewayTransactionId: "pi_update_fail",
            Metadata: new Dictionary<string, string>
            {
                ["type"] = "commerce_subscription",
                ["subscription_id"] = sub.Id.ToString(),
                ["tenant_id"] = _orgId.ToString(),
                ["update_payment"] = "1",
                ["failure_reason"] = "charge_declined",
                ["gateway_name"] = "STRIPE"
            });

        await _handler.HandleAsync(@event);

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters().FirstAsync(s => s.Id == sub.Id);
        reloaded.Status.Should().Be("ACTIVE");
        reloaded.CurrentDunningCampaignId.Should().BeNull();
        reloaded.NextBillingDate.Should().BeCloseTo(nextBilling, TimeSpan.FromSeconds(1));

        await _eventBus.DidNotReceive().PublishAsync(Arg.Is<OutboundWebhookRequestedIntegrationEvent>(e =>
            e.EventType == "subscription.past_due"));
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
        failed.DeclineClass.Should().Be("soft");
        failed.CompletedAt.Should().NotBeNull();

        var stillPending = await _db.ChargeAttemptLogs.FirstAsync(l => l.Id == attempt1.Id);
        stillPending.Status.Should().Be(ChargeAttemptLog.StatusPending);

        var hardEvent = new GatewayPaymentFailedIntegrationEvent(
            OrganizationId: _orgId,
            GatewayTransactionId: "off_session_hard:" + sub.Id,
            Metadata: new Dictionary<string, string>
            {
                ["subscription_id"] = sub.Id.ToString(),
                ["charge_attempt_id"] = attempt1.Id.ToString(),
                ["failure_reason"] = "stolen_card",
                ["decline_code"] = "stolen_card",
                ["gateway_name"] = "STRIPE"
            });
        await _handler.HandleAsync(hardEvent);
        var hardFailed = await _db.ChargeAttemptLogs.FirstAsync(l => l.Id == attempt1.Id);
        hardFailed.DeclineClass.Should().Be("hard");

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
        reloaded.TryGetDunningCampaignSnapshot()!.CampaignId.Should().Be(high.Id);
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

    [Test]
    public async Task HandleAsync_DoesNotPublishDispatchMessage()
    {
        var sub = new Subscription(_orgId, Guid.CreateVersion7(), _productId);
        sub.Activate(DateTime.UtcNow, DateTime.UtcNow);
        sub.StoreVaultedToken("c", "t");
        _db.Subscriptions.Add(sub);
        await _db.SaveChangesAsync();

        await _handler.HandleAsync(FailedEvent(sub.Id, "pi_no_comms"));

        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<Modules.Messaging.Contracts.DispatchMessageIntegrationEvent>());
    }

    [Test]
    public async Task HandleAsync_FirstFail_DispatchesDay0Email_DoesNotOffSession()
    {
        var product = CreateProduct(_orgId);
        var dueToday = DateTime.UtcNow.Date;
        var sub = new Subscription(_orgId, Guid.CreateVersion7(), product.Id);
        sub.Activate(dueToday.AddMonths(-1), dueToday);
        sub.StoreVaultedToken("cus_test", "pm_test");

        var campaign = Day0EmailCampaign(_orgId);
        campaign.AddStep(3, "EMAIL", "Day 3", "Still due", null);

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _handler.HandleAsync(FailedEvent(sub.Id, "pi_fail_day0"));

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.ReminderLogs)
            .FirstAsync(s => s.Id == sub.Id);
        reloaded.Status.Should().Be("PAST_DUE");
        reloaded.CurrentDunningCampaignId.Should().Be(campaign.Id);
        reloaded.LastCompletedDayOffset.Should().Be(0);
        reloaded.ReminderLogs.Should().ContainSingle(l =>
            l.DayOffset == 0 && l.TargetBillingDate.Date == dueToday);
        var snapshot = reloaded.TryGetDunningCampaignSnapshot();
        snapshot.Should().NotBeNull();
        snapshot!.Steps.Should().HaveCount(2);
        snapshot.Steps.Select(s => s.DayOffset).Should().Equal(0, 3);
        snapshot.Steps[0].EmailBody.Should().Be("Please pay");

        await _eventBus.Received(1).PublishAsync(Arg.Is<FulfillmentRequestedIntegrationEvent>(e =>
            e.InternalTargetApp == "COMMUNICATIONS"
            && e.EventType == "reminder.dunning"
            && e.Payload.GetProperty("subscription_id").GetString() == sub.Id.ToString()));
        await _eventBus.Received(1).PublishAsync(Arg.Is<OutboundWebhookRequestedIntegrationEvent>(e =>
            e.EventType == "subscription.past_due"));
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<ExecuteOffSessionChargeIntegrationEvent>());
    }

    [Test]
    public async Task HandleAsync_SecondFail_DoesNotDoubleDispatchDay0()
    {
        var product = CreateProduct(_orgId);
        var sub = new Subscription(_orgId, Guid.CreateVersion7(), product.Id);
        sub.Activate(DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow);
        sub.StoreVaultedToken("cus_test", "pm_test");
        var campaign = Day0EmailCampaign(_orgId);

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _handler.HandleAsync(FailedEvent(sub.Id, "pi_1"));
        await _handler.HandleAsync(FailedEvent(sub.Id, "pi_2"));

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.ReminderLogs)
            .FirstAsync(s => s.Id == sub.Id);
        reloaded.ReminderLogs.Should().ContainSingle(l => l.DayOffset == 0);

        await _eventBus.Received(1).PublishAsync(Arg.Is<FulfillmentRequestedIntegrationEvent>(e =>
            e.EventType == "reminder.dunning"));
        await _eventBus.Received(1).PublishAsync(Arg.Is<OutboundWebhookRequestedIntegrationEvent>(e =>
            e.EventType == "subscription.past_due"));
    }

    [Test]
    public async Task HandleAsync_AlreadyPastDueWithDay0Logged_DoesNotRedispatch()
    {
        var product = CreateProduct(_orgId);
        var due = DateTime.UtcNow.Date;
        var sub = new Subscription(_orgId, Guid.CreateVersion7(), product.Id);
        sub.Activate(due.AddMonths(-1), due);
        sub.StoreVaultedToken("cus", "pm");
        var campaign = Day0EmailCampaign(_orgId);
        sub.MarkAsPastDue();
        sub.AssignDunningCampaign(campaign.Id);
        sub.RecordReminderDispatched(campaign.Steps.Single().Id, due, 0);

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _handler.HandleAsync(FailedEvent(sub.Id, "pi_again"));

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.ReminderLogs)
            .FirstAsync(s => s.Id == sub.Id);
        reloaded.ReminderLogs.Should().HaveCount(1);
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<FulfillmentRequestedIntegrationEvent>());
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<OutboundWebhookRequestedIntegrationEvent>());
    }

    [Test]
    public async Task HandleAsync_Paused_AssignsButDoesNotDispatch()
    {
        var product = CreateProduct(_orgId);
        var sub = new Subscription(_orgId, Guid.CreateVersion7(), product.Id);
        sub.Activate(DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow);
        sub.StoreVaultedToken("cus", "pm");
        sub.PauseDunning(DateTime.UtcNow.AddDays(2));
        var campaign = Day0EmailCampaign(_orgId);

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _handler.HandleAsync(FailedEvent(sub.Id, "pi_paused"));

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.ReminderLogs)
            .FirstAsync(s => s.Id == sub.Id);
        reloaded.Status.Should().Be("PAST_DUE");
        reloaded.CurrentDunningCampaignId.Should().Be(campaign.Id);
        reloaded.ReminderLogs.Should().BeEmpty();
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<FulfillmentRequestedIntegrationEvent>());
        await _eventBus.Received(1).PublishAsync(Arg.Is<OutboundWebhookRequestedIntegrationEvent>(e =>
            e.EventType == "subscription.past_due"));
    }

    [Test]
    public async Task HandleAsync_NoMatchingCampaign_MarksPastDueWithoutComms()
    {
        var product = CreateProduct(_orgId);
        var sub = new Subscription(_orgId, Guid.CreateVersion7(), product.Id);
        sub.Activate(DateTime.UtcNow.AddMonths(-1), DateTime.UtcNow);
        sub.StoreVaultedToken("cus", "pm");
        var otherOrg = new DunningCampaign(Guid.CreateVersion7(), "Other", "NONE", 7, 1);
        otherOrg.AddStep(0, "EMAIL", "Hi", "Pay", null);

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(otherOrg);
        await _db.SaveChangesAsync();

        await _handler.HandleAsync(FailedEvent(sub.Id, "pi_none"));

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.ReminderLogs)
            .FirstAsync(s => s.Id == sub.Id);
        reloaded.Status.Should().Be("PAST_DUE");
        reloaded.CurrentDunningCampaignId.Should().BeNull();
        reloaded.ReminderLogs.Should().BeEmpty();
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<FulfillmentRequestedIntegrationEvent>());
        await _eventBus.Received(1).PublishAsync(Arg.Is<OutboundWebhookRequestedIntegrationEvent>(e =>
            e.EventType == "subscription.past_due"));
    }

    [Test]
    public async Task HandleAsync_AlreadyAssigned_LiveCampaignEditDoesNotRewriteSnapshot()
    {
        var product = CreateProduct(_orgId);
        var due = DateTime.UtcNow.Date;
        var sub = new Subscription(_orgId, Guid.CreateVersion7(), product.Id);
        sub.Activate(due.AddMonths(-1), due);
        sub.StoreVaultedToken("cus", "pm");
        var campaign = Day0EmailCampaign(_orgId);
        campaign.AddStep(3, "EMAIL", "Day 3", "Still due", null);

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _handler.HandleAsync(FailedEvent(sub.Id, "pi_first"));

        var assigned = await _db.Subscriptions.IgnoreQueryFilters().FirstAsync(s => s.Id == sub.Id);
        var frozenJson = assigned.DunningCampaignSnapshotJson;
        frozenJson.Should().NotBeNullOrWhiteSpace();
        var frozen = assigned.TryGetDunningCampaignSnapshot()!;

        campaign.UpdateDetails(campaign.Name, "NONE", gracePeriodDays: 1, campaign.PriorityOrder, null, null);
        campaign.ClearSteps();
        campaign.AddStep(0, "EMAIL", "Edited", "New body", null);
        campaign.AddStep(1, "EMAIL", "Catch-up", "Spam", null);
        await _db.SaveChangesAsync();

        await _handler.HandleAsync(FailedEvent(sub.Id, "pi_second"));

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.ReminderLogs)
            .FirstAsync(s => s.Id == sub.Id);
        reloaded.CurrentDunningCampaignId.Should().Be(campaign.Id);
        reloaded.DunningCampaignSnapshotJson.Should().Be(frozenJson);
        var stillFrozen = reloaded.TryGetDunningCampaignSnapshot()!;
        stillFrozen.GracePeriodDays.Should().Be(frozen.GracePeriodDays);
        stillFrozen.FinalAction.Should().Be(frozen.FinalAction);
        stillFrozen.Steps.Select(s => s.DayOffset).Should().Equal(0, 3);
        reloaded.ReminderLogs.Select(l => l.DayOffset).Should().Equal(0);
        reloaded.ReminderLogs.Should().NotContain(l => l.DayOffset == 1);
    }

    [Test]
    public async Task HandleAsync_ThreeDaysOverdue_CatchUpDispatchesOffset0And3()
    {
        var product = CreateProduct(_orgId);
        var due = DateTime.UtcNow.Date.AddDays(-3);
        var sub = new Subscription(_orgId, Guid.CreateVersion7(), product.Id);
        sub.Activate(due.AddMonths(-1), due);
        sub.StoreVaultedToken("cus", "pm");
        var campaign = Day0EmailCampaign(_orgId);
        campaign.AddStep(3, "EMAIL", "Day 3", "Still due", null);

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _handler.HandleAsync(FailedEvent(sub.Id, "pi_catchup"));

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.ReminderLogs)
            .FirstAsync(s => s.Id == sub.Id);
        reloaded.ReminderLogs.Select(l => l.DayOffset).Should().BeEquivalentTo(new[] { 0, 3 });
        reloaded.ReminderLogs.Should().OnlyContain(l => l.TargetBillingDate.Date == due);
        reloaded.LastCompletedDayOffset.Should().Be(3);

        await _eventBus.Received(2).PublishAsync(Arg.Is<FulfillmentRequestedIntegrationEvent>(e =>
            e.EventType == "reminder.dunning"));
    }

    private GatewayPaymentFailedIntegrationEvent FailedEvent(Guid subscriptionId, string gatewayTxId) =>
        new(
            OrganizationId: _orgId,
            GatewayTransactionId: gatewayTxId,
            Metadata: new Dictionary<string, string>
            {
                ["type"] = "commerce_subscription",
                ["subscription_id"] = subscriptionId.ToString(),
                ["tenant_id"] = _orgId.ToString(),
                ["failure_reason"] = "charge_declined",
                ["gateway_name"] = "STRIPE"
            });

    private static DunningCampaign Day0EmailCampaign(Guid orgId)
    {
        var campaign = new DunningCampaign(orgId, "Day0", "CANCEL", gracePeriodDays: 7, priorityOrder: 1);
        campaign.AddStep(0, "EMAIL", "Past due", "Please pay", null);
        return campaign;
    }

    private static Product CreateProduct(Guid orgId) =>
        new(
            orgId,
            "Plan",
            $"plan-{Guid.CreateVersion7():N}"[..20],
            50m,
            "FIXED",
            0m,
            "MYR",
            "mo",
            "STRIPE",
            new CheckoutConfiguration(false, false, false),
            Array.Empty<string>());
}
