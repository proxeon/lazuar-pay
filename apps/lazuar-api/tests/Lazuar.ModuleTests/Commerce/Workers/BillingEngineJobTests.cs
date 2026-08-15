using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using BuildingBlocks.Infrastructure.Configuration;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.ValueObjects;
using Modules.Commerce.Infrastructure;
using Modules.Commerce.Infrastructure.Workers;
using Modules.Payments.Contracts.Events;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce.Workers;

[TestFixture]
public class BillingEngineJobTests
{
    private CommerceDbContext _db = null!;
    private ServiceProvider _sp = null!;
    private BillingEngineJob _job = null!;
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
    public async Task RunOnce_SkipsPastDueAndCanceled()
    {
        var product = CreateProduct(_orgId);
        var pastDue = new Subscription(_orgId, Guid.CreateVersion7(), product.Id);
        pastDue.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-5));
        pastDue.MarkAsPastDue();

        var canceled = new Subscription(_orgId, Guid.CreateVersion7(), product.Id);
        canceled.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-5));
        canceled.Cancel();

        var activeDue = new Subscription(_orgId, Guid.CreateVersion7(), product.Id);
        activeDue.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-1));

        _db.Products.Add(product);
        _db.Subscriptions.AddRange(pastDue, canceled, activeDue);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        (await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == pastDue.Id))
            .Status.Should().Be("PAST_DUE");
        (await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == canceled.Id))
            .Status.Should().Be("CANCELED");
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
    public async Task RunOnce_StripeVaulted_PublishesOffSessionAttempt1()
    {
        var product = CreateProduct(_orgId, "STRIPE");
        var sub = new Subscription(_orgId, Guid.CreateVersion7(), product.Id);
        sub.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-1));
        sub.StoreVaultedToken("cus_live", "pm_live");

        _db.Products.Add(product);
        _db.Subscriptions.Add(sub);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        var reloaded = await _db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == sub.Id);
        reloaded.Status.Should().Be("ACTIVE");

        await _eventBus.Received(1).PublishAsync(Arg.Is<ExecuteOffSessionChargeIntegrationEvent>(e =>
            e.SubscriptionId == sub.Id
            && e.GatewayName == "STRIPE"
            && e.GatewayCustomerId == "cus_live"
            && e.GatewayTokenId == "pm_live"
            && e.ChargeAttemptId != null));

        var attempts = await _db.ChargeAttemptLogs.IgnoreQueryFilters()
            .Where(l => l.SubscriptionId == sub.Id)
            .ToListAsync();
        attempts.Should().HaveCount(1);
        attempts[0].AttemptNumber.Should().Be(1);
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

    private static Product CreateProduct(Guid orgId, string gatewayName = "STRIPE") =>
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
