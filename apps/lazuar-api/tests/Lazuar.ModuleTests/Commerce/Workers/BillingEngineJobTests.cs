using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using BuildingBlocks.Infrastructure.Configuration;
using FluentAssertions;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Commerce.Contracts.Events;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.Entities;
using Modules.Commerce.Domain.ValueObjects;
using Modules.Commerce.Infrastructure;
using Modules.Commerce.Infrastructure.Workers;
using Modules.CRM.Contracts;
using Modules.One.Contracts;
using Modules.Payments.Contracts.Events;
using Modules.Payments.Contracts.Queries;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce.Workers;

[TestFixture]
public class BillingEngineJobTests
{
    private CommerceDbContext _db = null!;
    private ServiceProvider _sp = null!;
    private BillingEngineJob _job = null!;
    private IEventBus _eventBus = null!;
    private IMediator _mediator = null!;
    private ICrmQueryService _crm = null!;
    private IOneQueryService _one = null!;
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
        _mediator = Substitute.For<IMediator>();
        _crm = Substitute.For<ICrmQueryService>();
        _one = Substitute.For<IOneQueryService>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:ClientUrl"] = "https://portal.test"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton(_db);
        services.AddKeyedSingleton<IEventBus>("CommerceEventBus", _eventBus);
        services.AddSingleton(_mediator);
        services.AddSingleton(_crm);
        services.AddSingleton(_one);
        services.AddSingleton<IConfiguration>(config);
        _sp = services.BuildServiceProvider();

        _job = new BillingEngineJob(
            _sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<BillingEngineJob>.Instance,
            Options.Create(new BackgroundWorkerOptions()));
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        _sp.Dispose();
    }

    [Test]
    public async Task RunOnce_MarksEachDueSubscriptionPastDue_Independently()
    {
        var product = CreateProduct(_orgId);
        var subA = new Subscription(_orgId, Guid.CreateVersion7(), product.Id);
        subA.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-1));
        var subB = new Subscription(_orgId, Guid.CreateVersion7(), product.Id);
        subB.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddHours(-2));

        _db.Products.Add(product);
        _db.Subscriptions.AddRange(subA, subB);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        var a = await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == subA.Id);
        var b = await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == subB.Id);
        a.Status.Should().Be("PAST_DUE");
        b.Status.Should().Be("PAST_DUE");
    }

    [Test]
    public async Task RunOnce_SkipsPastDueSuspendedCanceledAndFutureNotDue()
    {
        var product = CreateProduct(_orgId);
        var pastDue = new Subscription(_orgId, Guid.CreateVersion7(), product.Id);
        pastDue.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-5));
        pastDue.MarkAsPastDue();

        var canceled = new Subscription(_orgId, Guid.CreateVersion7(), product.Id);
        canceled.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-5));
        canceled.Cancel();

        var suspended = new Subscription(_orgId, Guid.CreateVersion7(), product.Id);
        suspended.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-5));
        suspended.Suspend();

        var future = new Subscription(_orgId, Guid.CreateVersion7(), product.Id);
        future.Activate(DateTime.UtcNow.AddMonths(1), DateTime.UtcNow.AddDays(10));

        var pending = new Subscription(_orgId, Guid.CreateVersion7(), product.Id);

        var activeDue = new Subscription(_orgId, Guid.CreateVersion7(), product.Id);
        activeDue.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-1));

        _db.Products.Add(product);
        _db.Subscriptions.AddRange(pastDue, canceled, suspended, future, pending, activeDue);
        await _db.SaveChangesAsync();

        _db.Entry(pending).Property(s => s.NextBillingDate).CurrentValue = DateTime.UtcNow.AddDays(-1);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        (await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == pastDue.Id))
            .Status.Should().Be("PAST_DUE");
        (await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == canceled.Id))
            .Status.Should().Be("CANCELED");
        (await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == suspended.Id))
            .Status.Should().Be("SUSPENDED");
        (await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == future.Id))
            .Status.Should().Be("ACTIVE");
        (await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == pending.Id))
            .Status.Should().Be("PENDING");
        (await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == activeDue.Id))
            .Status.Should().Be("PAST_DUE");
    }

    [Test]
    public async Task RunOnce_BillplzOrReminderOnlyOrNoVault_MarksPastDue_DoesNotPublishOffSession()
    {
        var billplz = CreateProduct(_orgId, "BILLPLZ");
        var billplzSub = new Subscription(_orgId, Guid.CreateVersion7(), billplz.Id);
        billplzSub.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-1));
        billplzSub.StoreVaultedToken("cus_junk", "tok_junk");

        var reminder = CreateProduct(_orgId, "STRIPE");
        var reminderSub = new Subscription(_orgId, Guid.CreateVersion7(), reminder.Id);
        reminderSub.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-1), isReminderOnly: true);

        var noVault = CreateProduct(_orgId, "STRIPE");
        var noVaultSub = new Subscription(_orgId, Guid.CreateVersion7(), noVault.Id);
        noVaultSub.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-1));

        _db.Products.AddRange(billplz, reminder, noVault);
        _db.Subscriptions.AddRange(billplzSub, reminderSub, noVaultSub);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        (await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == billplzSub.Id))
            .Status.Should().Be("PAST_DUE");
        (await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == reminderSub.Id))
            .Status.Should().Be("PAST_DUE");
        (await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == noVaultSub.Id))
            .Status.Should().Be("PAST_DUE");

        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<ExecuteOffSessionChargeIntegrationEvent>());
    }

    [Test]
    public async Task RunOnce_StripeVaulted_PublishesOffSessionAttempt1_DoesNotAdvanceDates()
    {
        var product = CreateProduct(_orgId, "STRIPE");
        var sub = new Subscription(_orgId, Guid.CreateVersion7(), product.Id);
        var nextBilling = DateTime.UtcNow.AddDays(-1);
        sub.Activate(DateTime.UtcNow.AddDays(-40), nextBilling);
        sub.StoreVaultedToken("cus_live", "pm_live");

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == sub.Id);
        reloaded.Status.Should().Be("ACTIVE");
        reloaded.NextBillingDate.Should().BeCloseTo(nextBilling, TimeSpan.FromSeconds(1));

        var attempts = await _db.ChargeAttemptLogs.IgnoreQueryFilters()
            .Where(l => l.SubscriptionId == sub.Id)
            .ToListAsync();
        attempts.Should().HaveCount(1);
        attempts[0].AttemptNumber.Should().Be(1);
        attempts[0].Source.Should().Be(ChargeAttemptLog.SourceBilling);
        attempts[0].Status.Should().Be(ChargeAttemptLog.StatusPending);

        await _eventBus.Received(1).PublishAsync(Arg.Is<ExecuteOffSessionChargeIntegrationEvent>(e =>
            e.SubscriptionId == sub.Id
            && e.Amount == product.Price
            && e.GatewayName == "STRIPE"
            && e.GatewayCustomerId == "cus_live"
            && e.GatewayTokenId == "pm_live"
            && e.DunningCampaignId == null
            && e.ChargeAttemptId == attempts[0].Id));
    }

    [Test]
    public async Task RunOnce_VaultedAlreadyHasAttempt1_DoesNotPublishAgain()
    {
        var product = CreateProduct(_orgId, "STRIPE");
        var sub = new Subscription(_orgId, Guid.CreateVersion7(), product.Id);
        var nextBilling = DateTime.UtcNow.AddDays(-1);
        sub.Activate(DateTime.UtcNow.AddDays(-40), nextBilling);
        sub.StoreVaultedToken("cus_live", "pm_live");

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.ChargeAttemptLogs.Add(new ChargeAttemptLog(
            sub.Id,
            nextBilling.Date,
            attemptNumber: 1,
            source: ChargeAttemptLog.SourceBilling));
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == sub.Id);
        reloaded.Status.Should().Be("ACTIVE");

        (await _db.ChargeAttemptLogs.IgnoreQueryFilters().CountAsync(l => l.SubscriptionId == sub.Id))
            .Should().Be(1);
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<ExecuteOffSessionChargeIntegrationEvent>());
    }

    [Test]
    public async Task RunOnce_ChipVaulted_PublishesOffSessionWithChipGateway()
    {
        var product = CreateProduct(_orgId, "CHIP");
        var sub = new Subscription(_orgId, Guid.CreateVersion7(), product.Id);
        sub.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-1));
        sub.StoreVaultedToken("purchase_old", "purchase_old");

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        await _eventBus.Received(1).PublishAsync(Arg.Is<ExecuteOffSessionChargeIntegrationEvent>(e =>
            e.GatewayName == "CHIP" && e.SubscriptionId == sub.Id));
    }

    [Test]
    public async Task RunOnce_NonVaultedDue_MintsCheckoutBoundToExistingSubscription_ThenPastDue()
    {
        var product = CreateProduct(_orgId, "BILLPLZ");
        var clientId = Guid.CreateVersion7();
        var sub = new Subscription(_orgId, clientId, product.Id);
        var nextBilling = DateTime.UtcNow.AddDays(-1);
        sub.Activate(DateTime.UtcNow.AddDays(-40), nextBilling, isReminderOnly: true);

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        await _db.SaveChangesAsync();

        ArrangeMint("buyer@example.com", "https://pay.test/bills/renew-1");

        await _job.RunOnceAsync(CancellationToken.None);

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == sub.Id);
        reloaded.Status.Should().Be("PAST_DUE");
        reloaded.CurrentRenewalCheckoutUrl.Should().Be("https://pay.test/bills/renew-1");
        reloaded.CurrentRenewalCheckoutForDate.Should().Be(nextBilling.Date);
        reloaded.NextBillingDate.Should().BeCloseTo(nextBilling, TimeSpan.FromSeconds(1));

        await _mediator.Received(1).Send(Arg.Is<GenerateCheckoutSessionQuery>(q =>
            q.TenantId == _orgId
            && q.Amount == product.Price
            && q.Currency == product.Currency
            && q.SetupFutureUsage
            && q.GatewayName == "BILLPLZ"
            && q.Metadata["type"] == "commerce_subscription"
            && q.Metadata["subscription_id"] == sub.Id.ToString()
            && q.Metadata["tenant_id"] == _orgId.ToString()
            && q.SuccessUrl == "https://portal.test/acme/portal"
            && q.CancelUrl == $"https://portal.test/acme/update-payment/{sub.Id}"),
            Arg.Any<CancellationToken>());

        await _eventBus.Received().PublishAsync(Arg.Is<OutboundWebhookRequestedIntegrationEvent>(e =>
            e.EventType == "subscription.past_due"
            && e.Payload.GetProperty("subscription_id").GetString() == sub.Id.ToString()
            && e.Payload.GetProperty("checkout_url").GetString() == "https://pay.test/bills/renew-1"));

        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<ExecuteOffSessionChargeIntegrationEvent>());
        (await _db.CheckoutSessions.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task RunOnce_NonVaultedGenerateThrows_DoesNotMarkPastDue_RetriesNextTick()
    {
        var product = CreateProduct(_orgId, "BILLPLZ");
        var clientId = Guid.CreateVersion7();
        var sub = new Subscription(_orgId, clientId, product.Id);
        sub.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-1), isReminderOnly: true);

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        await _db.SaveChangesAsync();

        _crm.GetClientProfileAsync(clientId).Returns(new ClientProfileDto
        {
            Id = clientId.ToString(),
            Full_name = "Buyer",
            Email = "buyer@example.com"
        });
        _one.GetWorkspaceByIdAsync(_orgId).Returns(
            new WorkspaceSnapshotDto(_orgId, "Acme", "acme", true, DateTime.UtcNow));
        _mediator.Send(Arg.Any<GenerateCheckoutSessionQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("gateway down"));

        await _job.RunOnceAsync(CancellationToken.None);

        var afterFirst = await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == sub.Id);
        afterFirst.Status.Should().Be("ACTIVE");
        afterFirst.CurrentRenewalCheckoutUrl.Should().BeNull();

        await _job.RunOnceAsync(CancellationToken.None);

        var afterSecond = await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == sub.Id);
        afterSecond.Status.Should().Be("ACTIVE");
        afterSecond.CurrentRenewalCheckoutUrl.Should().BeNull();

        await _mediator.Received(2).Send(Arg.Any<GenerateCheckoutSessionQuery>(), Arg.Any<CancellationToken>());
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<OutboundWebhookRequestedIntegrationEvent>());
    }

    [Test]
    public async Task RunOnce_NoToken_AssignsCampaignAndDispatchesDay0()
    {
        var product = CreateProduct(_orgId, "BILLPLZ");
        var nextBilling = DateTime.UtcNow.AddDays(-1);
        var sub = new Subscription(_orgId, Guid.CreateVersion7(), product.Id);
        sub.Activate(DateTime.UtcNow.AddDays(-40), nextBilling, isReminderOnly: true);
        var campaign = Day0EmailCampaign(_orgId);

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.ReminderLogs)
            .SingleAsync(s => s.Id == sub.Id);
        reloaded.Status.Should().Be("PAST_DUE");
        reloaded.CurrentDunningCampaignId.Should().Be(campaign.Id);
        reloaded.LastCompletedDayOffset.Should().Be(0);
        reloaded.ReminderLogs.Should().ContainSingle(l =>
            l.DayOffset == 0 && l.TargetBillingDate.Date == nextBilling.Date);

        await _eventBus.Received(1).PublishAsync(Arg.Is<FulfillmentRequestedIntegrationEvent>(e =>
            e.InternalTargetApp == "COMMUNICATIONS"
            && e.EventType == "reminder.dunning"
            && e.Payload.GetProperty("subscription_id").GetString() == sub.Id.ToString()));
        await _eventBus.Received().PublishAsync(Arg.Is<OutboundWebhookRequestedIntegrationEvent>(e =>
            e.EventType == "subscription.past_due"));
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<ExecuteOffSessionChargeIntegrationEvent>());
    }

    [Test]
    public async Task RunOnce_TwoNoToken_BothGetDay0()
    {
        var product = CreateProduct(_orgId);
        var subA = new Subscription(_orgId, Guid.CreateVersion7(), product.Id);
        subA.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-2));
        var subB = new Subscription(_orgId, Guid.CreateVersion7(), product.Id);
        subB.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-1));
        var campaign = Day0EmailCampaign(_orgId);

        _db.Products.Add(product);
        _db.Subscriptions.AddRange(subA, subB);
        _db.DunningCampaigns.Add(campaign);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        var a = await _db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.ReminderLogs).SingleAsync(s => s.Id == subA.Id);
        var b = await _db.Subscriptions.IgnoreQueryFilters()
            .Include(s => s.ReminderLogs).SingleAsync(s => s.Id == subB.Id);
        a.Status.Should().Be("PAST_DUE");
        b.Status.Should().Be("PAST_DUE");
        a.ReminderLogs.Should().ContainSingle(l => l.DayOffset == 0);
        b.ReminderLogs.Should().ContainSingle(l => l.DayOffset == 0);

        await _eventBus.Received(1).PublishAsync(Arg.Is<FulfillmentRequestedIntegrationEvent>(e =>
            e.EventType == "reminder.dunning"
            && e.Payload.GetProperty("subscription_id").GetString() == subA.Id.ToString()));
        await _eventBus.Received(1).PublishAsync(Arg.Is<FulfillmentRequestedIntegrationEvent>(e =>
            e.EventType == "reminder.dunning"
            && e.Payload.GetProperty("subscription_id").GetString() == subB.Id.ToString()));
    }

    [Test]
    public async Task RunOnce_OneTimeProduct_DoesNotPastDueOrCharge()
    {
        var oneTime = CreateProduct(_orgId, "STRIPE", "one_time");
        var recurring = CreateProduct(_orgId, "STRIPE");
        var oneTimeSub = new Subscription(_orgId, Guid.CreateVersion7(), oneTime.Id);
        oneTimeSub.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-2));
        oneTimeSub.StoreVaultedToken("cus", "pm");

        var dueRecurring = new Subscription(_orgId, Guid.CreateVersion7(), recurring.Id);
        dueRecurring.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-1));

        _db.Products.AddRange(oneTime, recurring);
        _db.Subscriptions.AddRange(oneTimeSub, dueRecurring);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        (await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == oneTimeSub.Id))
            .Status.Should().Be("ACTIVE");
        (await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == dueRecurring.Id))
            .Status.Should().Be("PAST_DUE");

        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<ExecuteOffSessionChargeIntegrationEvent>());
        (await _db.ChargeAttemptLogs.IgnoreQueryFilters().CountAsync(l => l.SubscriptionId == oneTimeSub.Id))
            .Should().Be(0);
    }

    [Test]
    public async Task RunOnce_MissingProduct_DoesNotThrowBatch_SiblingStillProcessed()
    {
        var product = CreateProduct(_orgId);
        var orphan = new Subscription(_orgId, Guid.CreateVersion7(), Guid.CreateVersion7());
        orphan.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-2));

        var sibling = new Subscription(_orgId, Guid.CreateVersion7(), product.Id);
        sibling.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-1));

        _db.Products.Add(product);
        _db.Subscriptions.AddRange(orphan, sibling);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        (await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == orphan.Id))
            .Status.Should().Be("ACTIVE");
        (await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == sibling.Id))
            .Status.Should().Be("PAST_DUE");
    }

    private void ArrangeMint(string email, string checkoutUrl)
    {
        _crm.GetClientProfileAsync(Arg.Any<Guid>()).Returns(new ClientProfileDto
        {
            Id = Guid.CreateVersion7().ToString(),
            Full_name = "Buyer",
            Email = email
        });
        _one.GetWorkspaceByIdAsync(_orgId).Returns(
            new WorkspaceSnapshotDto(_orgId, "Acme", "acme", true, DateTime.UtcNow));
        _mediator.Send(Arg.Any<GenerateCheckoutSessionQuery>(), Arg.Any<CancellationToken>())
            .Returns(checkoutUrl);
    }

    private static DunningCampaign Day0EmailCampaign(Guid orgId)
    {
        var campaign = new DunningCampaign(orgId, "Day0", "CANCEL", gracePeriodDays: 7, priorityOrder: 1);
        campaign.AddStep(0, "EMAIL", "Past due", "Please pay", null);
        return campaign;
    }

    private static Product CreateProduct(Guid orgId, string gatewayName = "STRIPE", string interval = "mo") =>
        new(
            orgId,
            "Plan",
            $"plan-{Guid.CreateVersion7():N}"[..20],
            50m,
            "FIXED",
            0m,
            "MYR",
            interval,
            gatewayName,
            new CheckoutConfiguration(false, false, false),
            Array.Empty<string>());
}
