using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using BuildingBlocks.Infrastructure.Configuration;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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

        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
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

    private static Subscription PastDueSub(Guid orgId, Guid productId, bool isReminderOnly = false)
    {
        var sub = new Subscription(orgId, Guid.CreateVersion7(), productId);
        sub.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-1), isReminderOnly);
        sub.MarkAsPastDue();
        return sub;
    }

    private static DunningCampaign AutoChargeCampaign(Guid orgId)
    {
        var campaign = new DunningCampaign(orgId, "Retry", "CANCEL", gracePeriodDays: 7, priorityOrder: 1);
        campaign.AddStep(0, "AUTO_CHARGE", null, null, null);
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
