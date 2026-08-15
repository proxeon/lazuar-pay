using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Application.Observability;
using BuildingBlocks.Infrastructure;
using BuildingBlocks.Infrastructure.Configuration;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Commerce.Contracts.Events;
using Modules.Commerce.Domain;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.Entities;
using Modules.Commerce.Domain.ValueObjects;
using Modules.Commerce.Infrastructure;
using Modules.Commerce.Infrastructure.Dunning;
using Modules.Commerce.Infrastructure.Workers;
using Modules.Payments.Contracts.Events;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce.Workers;

[TestFixture]
public class DunningEngineJobTests
{
    private CommerceDbContext _db = null!;
    private ServiceProvider _sp = null!;
    private DunningEngineJob _job = null!;
    private IEventBus _eventBus = null!;
    private Guid _orgId = Guid.Empty;

    [SetUp]
    public void SetUp()
    {
        _orgId = Guid.CreateVersion7();
        var options = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var ctx = Substitute.For<IExecutionContextAccessor>();
        ctx.TenantId.Returns(Guid.Empty);
        _db = new CommerceDbContext(options, ctx, Substitute.For<IMediator>(), new DatabaseJobTrigger());

        _eventBus = Substitute.For<IEventBus>();
        var services = new ServiceCollection();
        services.AddSingleton(_db);
        services.AddKeyedSingleton<IEventBus>("CommerceEventBus", _eventBus);
        _sp = services.BuildServiceProvider();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Messaging:WhatsAppEnabled"] = "false"
            })
            .Build();
        _job = new DunningEngineJob(
            _sp.GetRequiredService<IServiceScopeFactory>(),
            configuration,
            NullLogger<DunningEngineJob>.Instance,
            Options.Create(new BackgroundWorkerOptions()));
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        _sp.Dispose();
    }

    [Test]
    public async Task PastDue_Day0Email_PublishesReminderDunningAndRecordsLog()
    {
        var product = CreateProduct(_orgId, "STRIPE");
        var sub = PastDueSub(_orgId, product.Id, daysOverdue: 0);
        var campaign = Day0EmailCampaign(_orgId);

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.ReminderLogs).SingleAsync(s => s.Id == sub.Id);
        reloaded.ReminderLogs.Should().ContainSingle(l => l.DayOffset == 0
            && l.TargetBillingDate.Date == sub.NextBillingDate!.Value.Date);

        await _eventBus.Received(1).PublishAsync(Arg.Is<FulfillmentRequestedIntegrationEvent>(e =>
            e.InternalTargetApp == "COMMUNICATIONS"
            && e.EventType == "reminder.dunning"
            && e.Payload.GetProperty("action_type").GetString() == "EMAIL"
            && e.Payload.GetProperty("email_body").GetString() == "Please pay"
            && e.Payload.GetProperty("subject").GetString() == "Past due"
            && e.Payload.GetProperty("plan_name").GetString() == "Plan"
            && e.Payload.GetProperty("client_profile_id").GetString() == sub.ClientProfileId.ToString()
            && e.Payload.GetProperty("subscription_id").GetString() == sub.Id.ToString()));
    }

    [Test]
    public async Task PastDue_Day0Email_SecondRunIsIdempotent()
    {
        var product = CreateProduct(_orgId, "STRIPE");
        var sub = PastDueSub(_orgId, product.Id);
        var campaign = Day0EmailCampaign(_orgId);

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);
        await _job.RunOnceAsync(CancellationToken.None);

        await _eventBus.Received(1).PublishAsync(Arg.Any<FulfillmentRequestedIntegrationEvent>());
        (await _db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.ReminderLogs).SingleAsync(s => s.Id == sub.Id))
            .ReminderLogs.Should().ContainSingle(l => l.DayOffset == 0);
    }

    [Test]
    public async Task PastDue_WhatsAppOnlyNoEmailBody_RecordsLogWithoutPublish()
    {
        var product = CreateProduct(_orgId, "STRIPE");
        var sub = PastDueSub(_orgId, product.Id, daysOverdue: 3);
        var campaign = new DunningCampaign(_orgId, "WA only", "CANCEL", gracePeriodDays: 7, priorityOrder: 1);
        campaign.AddStep(3, "WHATSAPP", null, null, "Hey, pay up");

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<FulfillmentRequestedIntegrationEvent>());
        (await _db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.ReminderLogs).SingleAsync(s => s.Id == sub.Id))
            .ReminderLogs.Should().ContainSingle(l => l.DayOffset == 3);
    }

    [Test]
    public async Task PreDunning_Minus3Email_DoesNotFireTenDaysOut_FiresAtThreeDays()
    {
        var product = CreateProduct(_orgId, "STRIPE");
        var tooEarly = new Subscription(_orgId, Guid.CreateVersion7(), product.Id);
        tooEarly.Activate(DateTime.UtcNow.AddDays(10), DateTime.UtcNow.Date.AddDays(10));
        var dueSoon = new Subscription(_orgId, Guid.CreateVersion7(), product.Id);
        dueSoon.Activate(DateTime.UtcNow.AddDays(3), DateTime.UtcNow.Date.AddDays(3));
        var campaign = new DunningCampaign(_orgId, "Pre", "CANCEL", gracePeriodDays: 7, priorityOrder: 1);
        campaign.AddStep(-3, "EMAIL", "Renews soon", "Your plan renews. {{update_payment_link}}", null);

        _db.Products.Add(product);
        _db.Subscriptions.AddRange(tooEarly, dueSoon);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        var earlyReloaded = await _db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.ReminderLogs).SingleAsync(s => s.Id == tooEarly.Id);
        var dueReloaded = await _db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.ReminderLogs).SingleAsync(s => s.Id == dueSoon.Id);

        earlyReloaded.ReminderLogs.Should().BeEmpty();
        dueReloaded.ReminderLogs.Should().ContainSingle(l => l.DayOffset == -3);

        await _eventBus.DidNotReceive().PublishAsync(Arg.Is<FulfillmentRequestedIntegrationEvent>(e =>
            e.EventType == "reminder.dunning"
            && e.Payload.GetProperty("subscription_id").GetString() == tooEarly.Id.ToString()));
        await _eventBus.Received(1).PublishAsync(Arg.Is<FulfillmentRequestedIntegrationEvent>(e =>
            e.InternalTargetApp == "COMMUNICATIONS"
            && e.EventType == "reminder.dunning"
            && e.Payload.GetProperty("subscription_id").GetString() == dueSoon.Id.ToString()
            && e.Payload.GetProperty("action_type").GetString() == "EMAIL"));
    }

    [Test]
    public async Task PastDue_EmailStep_NoMatchingCampaign_DoesNotPublish()
    {
        var product = CreateProduct(_orgId, "STRIPE");
        var sub = PastDueSub(_orgId, product.Id);
        var campaign = new DunningCampaign(
            _orgId,
            "Other product",
            "CANCEL",
            gracePeriodDays: 7,
            priorityOrder: 1,
            targetProductIds: new[] { Guid.CreateVersion7() });
        campaign.AddStep(0, "EMAIL", "Past due", "Please pay", null);

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<FulfillmentRequestedIntegrationEvent>());
        var reloaded = await _db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.ReminderLogs).SingleAsync(s => s.Id == sub.Id);
        reloaded.ReminderLogs.Should().BeEmpty();
        reloaded.CurrentDunningCampaignId.Should().BeNull();
    }

    [Test]
    public async Task PastDue_PausedUntilFuture_NotClaimed()
    {
        var product = CreateProduct(_orgId, "STRIPE");
        var sub = PastDueSub(_orgId, product.Id);
        sub.PauseDunning(DateTime.UtcNow.AddDays(2));
        var campaign = Day0EmailCampaign(_orgId);

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<FulfillmentRequestedIntegrationEvent>());
        var reloaded = await _db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.ReminderLogs).SingleAsync(s => s.Id == sub.Id);
        reloaded.ReminderLogs.Should().BeEmpty();
        reloaded.CurrentDunningCampaignId.Should().BeNull();
    }

    [Test]
    public async Task PastDue_AutoCharge_BillplzWithFakeVault_DoesNotPublish()
    {
        var product = CreateProduct(_orgId, "BILLPLZ");
        var sub = PastDueSub(_orgId, product.Id);
        sub.StoreVaultedToken("cus_junk", "tok_junk");
        var campaign = AutoChargeCampaign(_orgId);

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<ExecuteOffSessionChargeIntegrationEvent>());
        (await _db.Subscriptions.IgnoreQueryFilters().Include(s => s.ReminderLogs).SingleAsync(s => s.Id == sub.Id))
            .ReminderLogs.Should().HaveCount(1);
        (await _db.ChargeAttemptLogs.IgnoreQueryFilters().CountAsync(l => l.SubscriptionId == sub.Id))
            .Should().Be(0);
    }

    [Test]
    public async Task PastDue_AutoCharge_ReminderOnlyNoVault_DoesNotPublish_RecordsReminder()
    {
        var product = CreateProduct(_orgId, "STRIPE");
        var sub = PastDueSub(_orgId, product.Id, isReminderOnly: true);
        var campaign = AutoChargeCampaign(_orgId);

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<ExecuteOffSessionChargeIntegrationEvent>());
        (await _db.Subscriptions.IgnoreQueryFilters().Include(s => s.ReminderLogs).SingleAsync(s => s.Id == sub.Id))
            .ReminderLogs.Should().HaveCount(1);
    }

    [Test]
    public async Task RunOnce_TwoPastDue_BothGetDay0Dispatch()
    {
        var product = CreateProduct(_orgId, "STRIPE");
        var older = PastDueSub(_orgId, product.Id, daysOverdue: 5);
        var newer = PastDueSub(_orgId, product.Id, daysOverdue: 1);
        var campaign = Day0EmailCampaign(_orgId);

        _db.Products.Add(product);
        _db.Subscriptions.AddRange(older, newer);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        var a = await _db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.ReminderLogs).SingleAsync(s => s.Id == older.Id);
        var b = await _db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.ReminderLogs).SingleAsync(s => s.Id == newer.Id);

        a.CurrentDunningCampaignId.Should().Be(campaign.Id);
        b.CurrentDunningCampaignId.Should().Be(campaign.Id);
        a.ReminderLogs.Should().ContainSingle(l => l.DayOffset == 0 && l.TargetBillingDate.Date == older.NextBillingDate!.Value.Date);
        b.ReminderLogs.Should().ContainSingle(l => l.DayOffset == 0 && l.TargetBillingDate.Date == newer.NextBillingDate!.Value.Date);
        a.LastCompletedDayOffset.Should().Be(0);
        b.LastCompletedDayOffset.Should().Be(0);

        await _eventBus.Received(1).PublishAsync(Arg.Is<FulfillmentRequestedIntegrationEvent>(e =>
            e.InternalTargetApp == "COMMUNICATIONS"
            && e.EventType == "reminder.dunning"
            && e.Payload.GetProperty("subscription_id").GetString() == older.Id.ToString()));
        await _eventBus.Received(1).PublishAsync(Arg.Is<FulfillmentRequestedIntegrationEvent>(e =>
            e.InternalTargetApp == "COMMUNICATIONS"
            && e.EventType == "reminder.dunning"
            && e.Payload.GetProperty("subscription_id").GetString() == newer.Id.ToString()));
    }

    [Test]
    public async Task RunOnce_ProcessedId_NotRedispatchedInSameRun()
    {
        var product = CreateProduct(_orgId, "STRIPE");
        var sub = PastDueSub(_orgId, product.Id);
        var campaign = Day0EmailCampaign(_orgId);

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.ReminderLogs).SingleAsync(s => s.Id == sub.Id);
        reloaded.ReminderLogs.Should().ContainSingle(l => l.DayOffset == 0);
        await _eventBus.Received(1).PublishAsync(Arg.Is<FulfillmentRequestedIntegrationEvent>(e =>
            e.EventType == "reminder.dunning"));
    }

    [Test]
    public async Task RunOnce_PausedRowSkipped_UnpausedSiblingStillProcessed()
    {
        var product = CreateProduct(_orgId, "STRIPE");
        var paused = PastDueSub(_orgId, product.Id, daysOverdue: 4);
        paused.PauseDunning(DateTime.UtcNow.AddDays(1));
        var open = PastDueSub(_orgId, product.Id, daysOverdue: 1);
        var campaign = Day0EmailCampaign(_orgId);

        _db.Products.Add(product);
        _db.Subscriptions.AddRange(paused, open);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        var pausedReloaded = await _db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.ReminderLogs).SingleAsync(s => s.Id == paused.Id);
        var openReloaded = await _db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.ReminderLogs).SingleAsync(s => s.Id == open.Id);

        pausedReloaded.ReminderLogs.Should().BeEmpty();
        pausedReloaded.CurrentDunningCampaignId.Should().BeNull();
        openReloaded.ReminderLogs.Should().ContainSingle(l => l.DayOffset == 0);
        openReloaded.CurrentDunningCampaignId.Should().Be(campaign.Id);

        await _eventBus.Received(1).PublishAsync(Arg.Is<FulfillmentRequestedIntegrationEvent>(e =>
            e.EventType == "reminder.dunning"
            && e.Payload.GetProperty("subscription_id").GetString() == open.Id.ToString()));
        await _eventBus.DidNotReceive().PublishAsync(Arg.Is<FulfillmentRequestedIntegrationEvent>(e =>
            e.EventType == "reminder.dunning"
            && e.Payload.GetProperty("subscription_id").GetString() == paused.Id.ToString()));
    }

    [Test]
    public async Task PastDue_AutoCharge_StripeVault_PublishesStripeGateway()
    {
        var product = CreateProduct(_orgId, "STRIPE");
        var sub = PastDueSub(_orgId, product.Id);
        sub.StoreVaultedToken("cus_live", "pm_live");
        var campaign = AutoChargeCampaign(_orgId);

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        await _eventBus.Received(1).PublishAsync(Arg.Is<ExecuteOffSessionChargeIntegrationEvent>(e =>
            e.SubscriptionId == sub.Id
            && e.GatewayName == "STRIPE"
            && e.GatewayCustomerId == "cus_live"
            && e.GatewayTokenId == "pm_live"
            && e.DunningCampaignId == campaign.Id));
    }

    [Test]
    public async Task PastDue_VaultedStripe_AutoChargeDue_PublishesAttempt2()
    {
        var product = CreateProduct(_orgId, "STRIPE");
        var sub = PastDueSub(_orgId, product.Id);
        sub.StoreVaultedToken("cus_live", "pm_live");
        var campaign = AutoChargeCampaign(_orgId, dayOffset: 1);
        var target = sub.NextBillingDate!.Value.Date;
        var billingAttempt = FailedBillingAttempt(sub.Id, target);

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        _db.ChargeAttemptLogs.Add(billingAttempt);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        var attempts = await _db.ChargeAttemptLogs.IgnoreQueryFilters()
            .Where(l => l.SubscriptionId == sub.Id)
            .OrderBy(l => l.AttemptNumber)
            .ToListAsync();
        attempts.Should().HaveCount(2);
        attempts[1].AttemptNumber.Should().Be(2);
        attempts[1].Source.Should().Be(ChargeAttemptLog.SourceDunning);
        attempts[1].Status.Should().Be(ChargeAttemptLog.StatusPending);
        attempts[1].DunningCampaignId.Should().Be(campaign.Id);

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.ReminderLogs).SingleAsync(s => s.Id == sub.Id);
        reloaded.ReminderLogs.Should().ContainSingle(l => l.DayOffset == 1 && l.TargetBillingDate.Date == target);

        await _eventBus.Received(1).PublishAsync(Arg.Is<ExecuteOffSessionChargeIntegrationEvent>(e =>
            e.SubscriptionId == sub.Id
            && e.GatewayName == "STRIPE"
            && e.DunningCampaignId == campaign.Id
            && e.ChargeAttemptId == attempts[1].Id));
    }

    [Test]
    public async Task PastDue_VaultedChip_UsesProductGatewayName()
    {
        var product = CreateProduct(_orgId, "CHIP");
        var sub = PastDueSub(_orgId, product.Id);
        sub.StoreVaultedToken("client_live", "tok_live");
        var campaign = AutoChargeCampaign(_orgId, dayOffset: 1);
        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        _db.ChargeAttemptLogs.Add(FailedBillingAttempt(sub.Id, sub.NextBillingDate!.Value.Date));
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        await _eventBus.Received(1).PublishAsync(Arg.Is<ExecuteOffSessionChargeIntegrationEvent>(e =>
            e.SubscriptionId == sub.Id
            && e.GatewayName == "CHIP"
            && e.DunningCampaignId == campaign.Id
            && e.ChargeAttemptId != null));
    }

    [Test]
    public async Task PastDue_Razorpay_DoesNotPublish()
    {
        var product = CreateProduct(_orgId, "RAZORPAY");
        var sub = PastDueSub(_orgId, product.Id);
        sub.StoreVaultedToken("cus_junk", "tok_junk");
        var campaign = AutoChargeCampaign(_orgId);

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<ExecuteOffSessionChargeIntegrationEvent>());
        (await _db.ChargeAttemptLogs.IgnoreQueryFilters().CountAsync(l => l.SubscriptionId == sub.Id))
            .Should().Be(0);
        (await _db.Subscriptions.IgnoreQueryFilters().Include(s => s.ReminderLogs).SingleAsync(s => s.Id == sub.Id))
            .ReminderLogs.Should().HaveCount(1);
    }

    [Test]
    public async Task PastDue_NoVault_DoesNotPublish_ConsumesOffset()
    {
        var product = CreateProduct(_orgId, "STRIPE");
        var sub = PastDueSub(_orgId, product.Id);
        var campaign = AutoChargeCampaign(_orgId);

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<ExecuteOffSessionChargeIntegrationEvent>());
        (await _db.Subscriptions.IgnoreQueryFilters().Include(s => s.ReminderLogs).SingleAsync(s => s.Id == sub.Id))
            .ReminderLogs.Should().HaveCount(1);
        (await _db.ChargeAttemptLogs.IgnoreQueryFilters().CountAsync(l => l.SubscriptionId == sub.Id))
            .Should().Be(0);
    }

    [Test]
    public async Task PastDue_MaxAttempts_DoesNotPublish()
    {
        var product = CreateProduct(_orgId, "STRIPE");
        var sub = PastDueSub(_orgId, product.Id);
        sub.StoreVaultedToken("cus_live", "pm_live");
        var campaign = AutoChargeCampaign(_orgId, dayOffset: 1);
        var target = sub.NextBillingDate!.Value.Date;

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        for (var i = 1; i <= ChargeAttemptLimits.MaxAttemptsPerBillingCycle; i++)
        {
            var log = new ChargeAttemptLog(
                sub.Id,
                target,
                i,
                i == 1 ? ChargeAttemptLog.SourceBilling : ChargeAttemptLog.SourceDunning);
            log.MarkFailed("declined");
            _db.ChargeAttemptLogs.Add(log);
        }

        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<ExecuteOffSessionChargeIntegrationEvent>());
        (await _db.ChargeAttemptLogs.IgnoreQueryFilters().CountAsync(l => l.SubscriptionId == sub.Id))
            .Should().Be(ChargeAttemptLimits.MaxAttemptsPerBillingCycle);
        (await _db.Subscriptions.IgnoreQueryFilters().Include(s => s.ReminderLogs).SingleAsync(s => s.Id == sub.Id))
            .ReminderLogs.Should().ContainSingle(l => l.DayOffset == 1);
    }

    [Test]
    public async Task PastDue_PendingAttempt_DoesNotPublish_DoesNotConsumeOffset()
    {
        var product = CreateProduct(_orgId, "STRIPE");
        var sub = PastDueSub(_orgId, product.Id);
        sub.StoreVaultedToken("cus_live", "pm_live");
        var campaign = AutoChargeCampaign(_orgId, dayOffset: 1);
        var target = sub.NextBillingDate!.Value.Date;
        var pending = new ChargeAttemptLog(sub.Id, target, 2, ChargeAttemptLog.SourceDunning, campaign.Id);

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        _db.ChargeAttemptLogs.Add(FailedBillingAttempt(sub.Id, target));
        _db.ChargeAttemptLogs.Add(pending);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<ExecuteOffSessionChargeIntegrationEvent>());
        (await _db.ChargeAttemptLogs.IgnoreQueryFilters().CountAsync(l => l.SubscriptionId == sub.Id))
            .Should().Be(2);
        (await _db.Subscriptions.IgnoreQueryFilters().Include(s => s.ReminderLogs).SingleAsync(s => s.Id == sub.Id))
            .ReminderLogs.Should().BeEmpty();
    }

    [Test]
    public async Task PastDue_TwoAutoChargeOffsetsDue_OnlyOneChargeThisTick()
    {
        var product = CreateProduct(_orgId, "STRIPE");
        var sub = PastDueSub(_orgId, product.Id, daysOverdue: 5);
        sub.StoreVaultedToken("cus_live", "pm_live");
        var campaign = new DunningCampaign(_orgId, "Retry", "CANCEL", gracePeriodDays: 7, priorityOrder: 1);
        campaign.AddStep(1, "AUTO_CHARGE", null, null, null);
        campaign.AddStep(5, "AUTO_CHARGE", null, null, null);
        var target = sub.NextBillingDate!.Value.Date;

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        _db.ChargeAttemptLogs.Add(FailedBillingAttempt(sub.Id, target));
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        await _eventBus.Received(1).PublishAsync(Arg.Any<ExecuteOffSessionChargeIntegrationEvent>());
        var attempts = await _db.ChargeAttemptLogs.IgnoreQueryFilters()
            .Where(l => l.SubscriptionId == sub.Id)
            .ToListAsync();
        attempts.Should().HaveCount(2);
        attempts.Should().ContainSingle(l => l.AttemptNumber == 2 && l.Source == ChargeAttemptLog.SourceDunning);

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.ReminderLogs).SingleAsync(s => s.Id == sub.Id);
        reloaded.ReminderLogs.Should().ContainSingle(l => l.DayOffset == 1);
        reloaded.ReminderLogs.Should().NotContain(l => l.DayOffset == 5);
    }

    [Test]
    public async Task PastDue_AlreadyDispatchedOffset_IsIdempotent()
    {
        var product = CreateProduct(_orgId, "STRIPE");
        var sub = PastDueSub(_orgId, product.Id);
        sub.StoreVaultedToken("cus_live", "pm_live");
        var campaign = AutoChargeCampaign(_orgId, dayOffset: 1);
        var target = sub.NextBillingDate!.Value.Date;
        sub.RecordReminderDispatched(Guid.CreateVersion7(), target, 1);

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        _db.ChargeAttemptLogs.Add(FailedBillingAttempt(sub.Id, target));
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<ExecuteOffSessionChargeIntegrationEvent>());
        (await _db.ChargeAttemptLogs.IgnoreQueryFilters().CountAsync(l => l.SubscriptionId == sub.Id))
            .Should().Be(1);
        (await _db.Subscriptions.IgnoreQueryFilters().Include(s => s.ReminderLogs).SingleAsync(s => s.Id == sub.Id))
            .ReminderLogs.Should().ContainSingle(l => l.DayOffset == 1);
    }

    [Test]
    public async Task PastDue_GraceReached_DoesNotCancelOrChargeBeforeLastAutoChargeOffset()
    {
        var product = CreateProduct(_orgId, "STRIPE");
        var sub = PastDueSub(_orgId, product.Id, daysOverdue: 3);
        sub.StoreVaultedToken("cus_live", "pm_live");
        var campaign = new DunningCampaign(_orgId, "Short grace", "CANCEL", gracePeriodDays: 3, priorityOrder: 1);
        campaign.AddStep(5, "AUTO_CHARGE", null, null, null);

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        (await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == sub.Id))
            .Status.Should().Be("PAST_DUE");
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<ExecuteOffSessionChargeIntegrationEvent>());
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<SubscriptionCanceledIntegrationEvent>());
        (await _db.ChargeAttemptLogs.IgnoreQueryFilters().CountAsync(l => l.SubscriptionId == sub.Id))
            .Should().Be(0);
    }

    [TestCase(7, new[] { -3, 0, 3 }, 7)]
    [TestCase(3, new[] { 0, 7 }, 7)]
    [TestCase(0, new[] { 0 }, 0)]
    [TestCase(7, new[] { -3 }, 7)]
    [TestCase(3, new int[0], 3)]
    [TestCase(-1, new[] { 5 }, 5)]
    public void ResolveTerminalDayOffset_UsesLaterOfGraceAndLastPastDueStep(
        int grace, int[] offsets, int expected)
    {
        DunningEngineJob.ResolveTerminalDayOffset(grace, offsets).Should().Be(expected);
        PastDueDunningProcessor.ResolveTerminalDayOffset(grace, offsets).Should().Be(expected);
    }

    [Test]
    public async Task Cancel_AfterLastStep_WhenLastStepAfterGrace()
    {
        var product = CreateProduct(_orgId, "STRIPE");
        var campaign = EmailCampaign(_orgId, "CANCEL", grace: 3, dayOffset: 7);
        var sub = PastDueSub(_orgId, product.Id, daysOverdue: 7);
        sub.AssignDunningCampaign(campaign.Id);
        var cancelsBefore = LazuarMetrics.DunningCancelsTotal;

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.ReminderLogs).SingleAsync(s => s.Id == sub.Id);
        reloaded.Status.Should().Be("CANCELED");
        reloaded.ReminderLogs.Should().ContainSingle(l => l.DayOffset == 7);

        (await _db.DunningCampaigns.IgnoreQueryFilters().SingleAsync(c => c.Id == campaign.Id))
            .ChurnedSubscriptions.Should().Be(1);
        LazuarMetrics.DunningCancelsTotal.Should().Be(cancelsBefore + 1);

        await _eventBus.Received(1).PublishAsync(Arg.Is<FulfillmentRequestedIntegrationEvent>(e =>
            e.InternalTargetApp == "COMMUNICATIONS"
            && e.EventType == "reminder.dunning"
            && e.Payload.GetProperty("subscription_id").GetString() == sub.Id.ToString()
            && e.Payload.GetProperty("action_type").GetString() == "EMAIL"));
        await _eventBus.Received(1).PublishAsync(Arg.Is<SubscriptionCanceledIntegrationEvent>(e =>
            e.SubscriptionId == sub.Id && e.OrganizationId == _orgId));
    }

    [Test]
    public async Task Cancel_DoesNotFire_BeforeLastStep()
    {
        var product = CreateProduct(_orgId, "STRIPE");
        var campaign = EmailCampaign(_orgId, "CANCEL", grace: 3, dayOffset: 7);
        var sub = PastDueSub(_orgId, product.Id, daysOverdue: 3);
        sub.AssignDunningCampaign(campaign.Id);

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.ReminderLogs).SingleAsync(s => s.Id == sub.Id);
        reloaded.Status.Should().Be("PAST_DUE");
        reloaded.ReminderLogs.Should().BeEmpty();

        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<SubscriptionCanceledIntegrationEvent>());
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<FulfillmentRequestedIntegrationEvent>());
    }

    [Test]
    public async Task Suspend_AfterLastStep_SameDayAsGrace()
    {
        var product = CreateProduct(_orgId, "STRIPE");
        var campaign = EmailCampaign(_orgId, "SUSPEND", grace: 7, dayOffset: 3);
        var sub = PastDueSub(_orgId, product.Id, daysOverdue: 7);
        sub.AssignDunningCampaign(campaign.Id);

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.ReminderLogs).SingleAsync(s => s.Id == sub.Id);
        reloaded.Status.Should().Be("SUSPENDED");
        reloaded.SuspendedAt.Should().NotBeNull();
        reloaded.ReminderLogs.Should().ContainSingle(l => l.DayOffset == 3);

        (await _db.DunningCampaigns.IgnoreQueryFilters().SingleAsync(c => c.Id == campaign.Id))
            .ChurnedSubscriptions.Should().Be(0);

        await _eventBus.Received(1).PublishAsync(Arg.Is<SubscriptionSuspendedIntegrationEvent>(e =>
            e.SubscriptionId == sub.Id));
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<SubscriptionCanceledIntegrationEvent>());
    }

    [Test]
    public async Task GraceZero_DispatchesDayZeroThenCancels()
    {
        var product = CreateProduct(_orgId, "STRIPE");
        var campaign = EmailCampaign(_orgId, "CANCEL", grace: 0, dayOffset: 0);
        var sub = PastDueSub(_orgId, product.Id, daysOverdue: 0);
        sub.AssignDunningCampaign(campaign.Id);

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.ReminderLogs).SingleAsync(s => s.Id == sub.Id);
        reloaded.Status.Should().Be("CANCELED");
        reloaded.ReminderLogs.Should().ContainSingle(l => l.DayOffset == 0);

        await _eventBus.Received(1).PublishAsync(Arg.Is<FulfillmentRequestedIntegrationEvent>(e =>
            e.EventType == "reminder.dunning"
            && e.Payload.GetProperty("subscription_id").GetString() == sub.Id.ToString()));
        await _eventBus.Received(1).PublishAsync(Arg.Any<SubscriptionCanceledIntegrationEvent>());
    }

    [Test]
    public async Task None_DoesNotBlockLaterSteps()
    {
        var product = CreateProduct(_orgId, "STRIPE");
        var campaign = EmailCampaign(_orgId, "NONE", grace: 3, dayOffset: 7);
        var sub = PastDueSub(_orgId, product.Id, daysOverdue: 7);
        sub.AssignDunningCampaign(campaign.Id);

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.ReminderLogs).SingleAsync(s => s.Id == sub.Id);
        reloaded.Status.Should().Be("PAST_DUE");
        reloaded.ReminderLogs.Should().ContainSingle(l => l.DayOffset == 7);

        await _eventBus.Received(1).PublishAsync(Arg.Is<FulfillmentRequestedIntegrationEvent>(e =>
            e.EventType == "reminder.dunning"
            && e.Payload.GetProperty("subscription_id").GetString() == sub.Id.ToString()));
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<SubscriptionCanceledIntegrationEvent>());
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<SubscriptionSuspendedIntegrationEvent>());
    }

    [Test]
    public async Task Cancel_WhenNoPastDueSteps_OnGraceDay()
    {
        var product = CreateProduct(_orgId, "STRIPE");
        var campaign = new DunningCampaign(_orgId, "Empty timeline", "CANCEL", gracePeriodDays: 3, priorityOrder: 1);
        var sub = PastDueSub(_orgId, product.Id, daysOverdue: 3);
        sub.AssignDunningCampaign(campaign.Id);

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        (await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == sub.Id))
            .Status.Should().Be("CANCELED");
        await _eventBus.Received(1).PublishAsync(Arg.Any<SubscriptionCanceledIntegrationEvent>());
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<FulfillmentRequestedIntegrationEvent>());
    }

    [Test]
    public async Task Paused_SkipsTerminal()
    {
        var product = CreateProduct(_orgId, "STRIPE");
        var campaign = EmailCampaign(_orgId, "CANCEL", grace: 3, dayOffset: 7);
        var sub = PastDueSub(_orgId, product.Id, daysOverdue: 10);
        sub.AssignDunningCampaign(campaign.Id);
        sub.PauseDunning(DateTime.UtcNow.AddDays(1));

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.ReminderLogs).SingleAsync(s => s.Id == sub.Id);
        reloaded.Status.Should().Be("PAST_DUE");
        reloaded.ReminderLogs.Should().BeEmpty();
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<SubscriptionCanceledIntegrationEvent>());
    }

    [Test]
    public async Task UnknownActionType_Cancel_DoesNotCallDomainCancel()
    {
        var product = CreateProduct(_orgId, "STRIPE");
        var campaign = new DunningCampaign(_orgId, "Bogus step", "NONE", gracePeriodDays: 7, priorityOrder: 1);
        campaign.AddStep(0, "CANCEL", null, null, null);
        var sub = PastDueSub(_orgId, product.Id, daysOverdue: 0);
        sub.AssignDunningCampaign(campaign.Id);

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.ReminderLogs).SingleAsync(s => s.Id == sub.Id);
        reloaded.Status.Should().Be("PAST_DUE");
        reloaded.ReminderLogs.Should().ContainSingle(l => l.DayOffset == 0);

        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<SubscriptionCanceledIntegrationEvent>());
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<FulfillmentRequestedIntegrationEvent>());
    }

    [Test]
    public async Task AlreadyCanceled_NotReclaimed()
    {
        var product = CreateProduct(_orgId, "STRIPE");
        var campaign = EmailCampaign(_orgId, "CANCEL", grace: 0, dayOffset: 0);
        var sub = new Subscription(_orgId, Guid.CreateVersion7(), product.Id);
        sub.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.Date.AddDays(-10));
        sub.AssignDunningCampaign(campaign.Id);
        sub.Cancel();

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == sub.Id);
        reloaded.Status.Should().Be("CANCELED");
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<SubscriptionCanceledIntegrationEvent>());
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<FulfillmentRequestedIntegrationEvent>());
    }

    [Test]
    public async Task PreDunning_DoesNotAutoCharge()
    {
        var product = CreateProduct(_orgId, "STRIPE");
        var sub = new Subscription(_orgId, Guid.CreateVersion7(), product.Id);
        sub.Activate(DateTime.UtcNow.AddDays(3), DateTime.UtcNow.Date.AddDays(3));
        sub.StoreVaultedToken("cus_live", "pm_live");
        var campaign = new DunningCampaign(_orgId, "Early retry", "CANCEL", gracePeriodDays: 7, priorityOrder: 1);
        campaign.AddStep(-3, "AUTO_CHARGE", null, null, null);

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<ExecuteOffSessionChargeIntegrationEvent>());
        (await _db.ChargeAttemptLogs.IgnoreQueryFilters().CountAsync(l => l.SubscriptionId == sub.Id))
            .Should().Be(0);
        (await _db.Subscriptions.IgnoreQueryFilters().Include(s => s.ReminderLogs).SingleAsync(s => s.Id == sub.Id))
            .ReminderLogs.Should().BeEmpty();
    }

    private static Subscription PastDueSub(Guid orgId, Guid productId, bool isReminderOnly = false, int daysOverdue = 1)
    {
        var sub = new Subscription(orgId, Guid.CreateVersion7(), productId);
        sub.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.Date.AddDays(-daysOverdue), isReminderOnly);
        sub.MarkAsPastDue();
        return sub;
    }

    private static ChargeAttemptLog FailedBillingAttempt(Guid subscriptionId, DateTime targetDate)
    {
        var log = new ChargeAttemptLog(subscriptionId, targetDate, 1, ChargeAttemptLog.SourceBilling);
        log.MarkFailed("charge_declined", "STRIPE");
        return log;
    }

    private static DunningCampaign AutoChargeCampaign(Guid orgId, int dayOffset = 0)
    {
        var campaign = new DunningCampaign(orgId, "Retry", "CANCEL", gracePeriodDays: 7, priorityOrder: 1);
        campaign.AddStep(dayOffset, "AUTO_CHARGE", null, null, null);
        return campaign;
    }

    private static DunningCampaign Day0EmailCampaign(Guid orgId)
    {
        var campaign = new DunningCampaign(orgId, "Day0", "CANCEL", gracePeriodDays: 7, priorityOrder: 1);
        campaign.AddStep(0, "EMAIL", "Past due", "Please pay", null);
        return campaign;
    }

    private static DunningCampaign EmailCampaign(Guid orgId, string finalAction, int grace, int dayOffset)
    {
        var campaign = new DunningCampaign(orgId, "LP078", finalAction, gracePeriodDays: grace, priorityOrder: 1);
        campaign.AddStep(dayOffset, "EMAIL", "Past due", "Please pay", null);
        return campaign;
    }

    private static Product CreateProduct(Guid orgId, string gatewayName) =>
        new(
            orgId,
            "Plan",
            "plan",
            50m,
            "FIXED",
            0m,
            "MYR",
            "mo",
            gatewayName,
            new CheckoutConfiguration(false, false, false),
            Array.Empty<string>());
}
