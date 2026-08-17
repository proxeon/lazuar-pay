using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.Commerce.Application;
using Modules.Commerce.Contracts.Events;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.ValueObjects;
using Modules.Commerce.Infrastructure;
using Modules.Commerce.Infrastructure.EventHandlers;
using Modules.CRM.Contracts;
using Modules.Payments.Contracts.Events;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class GatewayPaymentCompletedRecoveryMetricsTests
{
    private const decimal RecoveryAmount = 49.90m;

    [Test]
    public async Task H1_PastDue_MetadataCampaignId_IncrementsOnce()
    {
        using var fx = await SeedPastDueAsync();
        var @event = PaymentEvent(fx, RecoveryAmount, new Dictionary<string, string>
        {
            ["type"] = "commerce_subscription",
            ["subscription_id"] = fx.Subscription.Id.ToString(),
            ["tenant_id"] = fx.OrgId.ToString(),
            ["dunning_campaign_id"] = fx.Campaign.Id.ToString()
        });

        await fx.Handler.HandleAsync(@event);

        await AssertRecoveredAsync(fx, expectedRevenue: RecoveryAmount, expectedSaved: 1);
        await fx.EventBus.Received(1).PublishAsync(Arg.Any<SubscriptionActivatedIntegrationEvent>());
        await fx.EventBus.DidNotReceive().PublishAsync(Arg.Any<SubscriptionResumedIntegrationEvent>());
    }

    [Test]
    public async Task H2_PastDue_BillplzStrippedMetadata_FallsBackToCurrentCampaignId()
    {
        using var fx = await SeedPastDueAsync();
        var @event = PaymentEvent(fx, RecoveryAmount, new Dictionary<string, string>
        {
            ["type"] = "commerce_subscription",
            ["subscription_id"] = fx.Subscription.Id.ToString(),
            ["tenant_id"] = fx.OrgId.ToString()
        });

        await fx.Handler.HandleAsync(@event);

        await AssertRecoveredAsync(fx, expectedRevenue: RecoveryAmount, expectedSaved: 1);
    }

    [Test]
    public async Task H3_OffSession_SubscriptionAndReceipt_Increments()
    {
        using var fx = await SeedPastDueAsync();
        var @event = PaymentEvent(fx, RecoveryAmount, new Dictionary<string, string>
        {
            ["type"] = "commerce_subscription",
            ["subscription_id"] = fx.Subscription.Id.ToString(),
            ["receipt"] = fx.Subscription.Id.ToString(),
            ["tenant_id"] = fx.OrgId.ToString(),
            ["dunning_campaign_id"] = fx.Campaign.Id.ToString()
        });

        await fx.Handler.HandleAsync(@event);

        await AssertRecoveredAsync(fx, expectedRevenue: RecoveryAmount, expectedSaved: 1);
    }

    [Test]
    public async Task H4_ReceiptOnly_StillRecoversAndIncrements()
    {
        using var fx = await SeedPastDueAsync();
        var @event = PaymentEvent(fx, RecoveryAmount, new Dictionary<string, string>
        {
            ["type"] = "commerce_subscription",
            ["receipt"] = fx.Subscription.Id.ToString(),
            ["tenant_id"] = fx.OrgId.ToString(),
            ["dunning_campaign_id"] = fx.Campaign.Id.ToString()
        });

        await fx.Handler.HandleAsync(@event);

        await AssertRecoveredAsync(fx, expectedRevenue: RecoveryAmount, expectedSaved: 1);
    }

    [Test]
    public async Task H5_ReplayAfterActive_DoesNotIncrementAgain()
    {
        using var fx = await SeedPastDueAsync();
        var @event = PaymentEvent(fx, RecoveryAmount, new Dictionary<string, string>
        {
            ["type"] = "commerce_subscription",
            ["subscription_id"] = fx.Subscription.Id.ToString(),
            ["tenant_id"] = fx.OrgId.ToString(),
            ["dunning_campaign_id"] = fx.Campaign.Id.ToString()
        });

        await fx.Handler.HandleAsync(@event);
        await fx.Handler.HandleAsync(@event);

        await AssertRecoveredAsync(fx, expectedRevenue: RecoveryAmount, expectedSaved: 1);
    }

    [Test]
    public async Task H12_SecondCompletionAfterRecover_DoesNotAdvanceDatesAgain()
    {
        using var fx = await SeedPastDueAsync();
        var first = PaymentEvent(fx, RecoveryAmount, new Dictionary<string, string>
        {
            ["type"] = "commerce_subscription",
            ["subscription_id"] = fx.Subscription.Id.ToString(),
            ["tenant_id"] = fx.OrgId.ToString(),
            ["dunning_campaign_id"] = fx.Campaign.Id.ToString()
        });
        await fx.Handler.HandleAsync(first);

        var afterFirst = await ReloadSubAsync(fx);
        var paidThrough = afterFirst.NextBillingDate;
        paidThrough.Should().NotBeNull();
        paidThrough!.Value.Should().BeAfter(DateTime.UtcNow);

        var second = new GatewayPaymentCompletedIntegrationEvent(
            OrganizationId: fx.OrgId,
            GatewayTransactionId: "pi_recovery_second",
            AmountPaid: RecoveryAmount,
            Currency: "MYR",
            GatewayFee: 1m,
            TaxAmount: 0m,
            NetAmount: RecoveryAmount - 1m,
            FxRate: 1m,
            BaseCurrency: "MYR",
            LineItems: new List<LineItemDto>(),
            Metadata: new Dictionary<string, string>
            {
                ["type"] = "commerce_subscription",
                ["subscription_id"] = fx.Subscription.Id.ToString(),
                ["tenant_id"] = fx.OrgId.ToString()
            });

        await fx.Handler.HandleAsync(second);

        var afterSecond = await ReloadSubAsync(fx);
        afterSecond.Status.Should().Be("ACTIVE");
        afterSecond.NextBillingDate.Should().BeCloseTo(paidThrough.Value, TimeSpan.FromSeconds(1));
        await AssertRecoveredAsync(fx, expectedRevenue: RecoveryAmount, expectedSaved: 1);
        await fx.EventBus.Received(1).PublishAsync(Arg.Any<SubscriptionActivatedIntegrationEvent>());
    }

    [Test]
    public async Task H6_ActiveRenewal_DoesNotIncrement()
    {
        using var fx = await SeedActiveAsync();
        var previousNextBilling = fx.Subscription.NextBillingDate;
        var @event = PaymentEvent(fx, RecoveryAmount, new Dictionary<string, string>
        {
            ["type"] = "commerce_subscription",
            ["subscription_id"] = fx.Subscription.Id.ToString(),
            ["tenant_id"] = fx.OrgId.ToString(),
            ["dunning_campaign_id"] = fx.Campaign.Id.ToString()
        });

        await fx.Handler.HandleAsync(@event);

        var sub = await ReloadSubAsync(fx);
        sub.Status.Should().Be("ACTIVE");
        sub.NextBillingDate.Should().NotBe(previousNextBilling);
        var campaign = await ReloadCampaignAsync(fx);
        campaign.RecoveredRevenue.Should().Be(0);
        campaign.SavedSubscriptions.Should().Be(0);
    }

    [Test]
    public async Task H7_Suspended_ResumesAndIncrements()
    {
        using var fx = await SeedSuspendedAsync();
        var @event = PaymentEvent(fx, RecoveryAmount, new Dictionary<string, string>
        {
            ["type"] = "commerce_subscription",
            ["subscription_id"] = fx.Subscription.Id.ToString(),
            ["tenant_id"] = fx.OrgId.ToString(),
            ["dunning_campaign_id"] = fx.Campaign.Id.ToString()
        });

        await fx.Handler.HandleAsync(@event);

        await AssertRecoveredAsync(fx, expectedRevenue: RecoveryAmount, expectedSaved: 1);
        await fx.EventBus.Received(1).PublishAsync(Arg.Any<SubscriptionResumedIntegrationEvent>());
        await fx.EventBus.DidNotReceive().PublishAsync(Arg.Any<SubscriptionActivatedIntegrationEvent>());
    }

    [Test]
    public async Task H8_NoCampaignAssigned_RecoversWithoutIncrement()
    {
        using var fx = await SeedPastDueAsync(assignCampaign: false);
        var @event = PaymentEvent(fx, RecoveryAmount, new Dictionary<string, string>
        {
            ["type"] = "commerce_subscription",
            ["subscription_id"] = fx.Subscription.Id.ToString(),
            ["tenant_id"] = fx.OrgId.ToString()
        });

        await fx.Handler.HandleAsync(@event);

        var sub = await ReloadSubAsync(fx);
        sub.Status.Should().Be("ACTIVE");
        var campaign = await ReloadCampaignAsync(fx);
        campaign.RecoveredRevenue.Should().Be(0);
        campaign.SavedSubscriptions.Should().Be(0);
    }

    [Test]
    public async Task H9_OtherOrgCampaignId_DoesNotIncrementThisOrg()
    {
        using var fx = await SeedPastDueAsync();
        var otherOrgCampaign = new DunningCampaign(Guid.CreateVersion7(), "Other org", "NONE", 7);
        fx.Db.DunningCampaigns.Add(otherOrgCampaign);
        await fx.Db.SaveChangesAsync();

        var @event = PaymentEvent(fx, RecoveryAmount, new Dictionary<string, string>
        {
            ["type"] = "commerce_subscription",
            ["subscription_id"] = fx.Subscription.Id.ToString(),
            ["tenant_id"] = fx.OrgId.ToString(),
            ["dunning_campaign_id"] = otherOrgCampaign.Id.ToString()
        });

        await fx.Handler.HandleAsync(@event);

        var sub = await ReloadSubAsync(fx);
        sub.Status.Should().Be("ACTIVE");
        var thisOrg = await ReloadCampaignAsync(fx);
        thisOrg.RecoveredRevenue.Should().Be(0);
        var other = await fx.Db.DunningCampaigns.IgnoreQueryFilters()
            .SingleAsync(c => c.Id == otherOrgCampaign.Id);
        other.RecoveredRevenue.Should().Be(0);
    }

    [Test]
    public async Task H10_DeletedCampaign_RecoversWithoutThrow()
    {
        using var fx = await SeedPastDueAsync(assignCampaign: false);
        var missingId = Guid.CreateVersion7();
        fx.Subscription.AssignDunningCampaign(missingId);
        await fx.Db.SaveChangesAsync();

        var @event = PaymentEvent(fx, RecoveryAmount, new Dictionary<string, string>
        {
            ["type"] = "commerce_subscription",
            ["subscription_id"] = fx.Subscription.Id.ToString(),
            ["tenant_id"] = fx.OrgId.ToString(),
            ["dunning_campaign_id"] = missingId.ToString()
        });

        var act = async () => await fx.Handler.HandleAsync(@event);
        await act.Should().NotThrowAsync();

        var sub = await ReloadSubAsync(fx);
        sub.Status.Should().Be("ACTIVE");
        var campaign = await ReloadCampaignAsync(fx);
        campaign.RecoveredRevenue.Should().Be(0);
    }

    [Test]
    public async Task H11_OpenCheckoutSession_DoesNotIncrementCampaign()
    {
        using var fx = await SeedPastDueAsync();
        var clientId = Guid.CreateVersion7();
        var session = new CheckoutSession(fx.OrgId, clientId, fx.Product.Id, couponId: null, DateTime.UtcNow.AddHours(1));
        fx.Db.CheckoutSessions.Add(session);
        await fx.Db.SaveChangesAsync();

        fx.Crm.GetClientProfileAsync(clientId).Returns(new ClientProfileDto
        {
            Id = clientId.ToString(),
            Full_name = "New Buyer",
            Email = "new@example.com"
        });

        var @event = PaymentEvent(fx, RecoveryAmount, new Dictionary<string, string>
        {
            ["type"] = "commerce_subscription",
            ["subscription_id"] = session.Id.ToString(),
            ["tenant_id"] = fx.OrgId.ToString(),
            ["dunning_campaign_id"] = fx.Campaign.Id.ToString()
        });

        await fx.Handler.HandleAsync(@event);

        var reloadedSession = await fx.Db.CheckoutSessions.IgnoreQueryFilters()
            .SingleAsync(s => s.Id == session.Id);
        reloadedSession.Status.Should().Be("COMPLETED");
        (await fx.Db.Subscriptions.IgnoreQueryFilters().CountAsync()).Should().Be(2);
        var campaign = await ReloadCampaignAsync(fx);
        campaign.RecoveredRevenue.Should().Be(0);
        campaign.SavedSubscriptions.Should().Be(0);
    }

    private static async Task<RecoveryFixture> SeedPastDueAsync(bool assignCampaign = true)
    {
        var fx = CreateFixture();
        fx.Subscription.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-10));
        fx.Subscription.MarkAsPastDue();
        if (assignCampaign)
        {
            fx.Subscription.AssignDunningCampaign(fx.Campaign.Id);
        }

        fx.Db.Products.Add(fx.Product);
        fx.Db.DunningCampaigns.Add(fx.Campaign);
        fx.Db.Subscriptions.Add(fx.Subscription);
        await fx.Db.SaveChangesAsync();
        return fx;
    }

    private static async Task<RecoveryFixture> SeedActiveAsync()
    {
        var fx = CreateFixture();
        fx.Subscription.Activate(DateTime.UtcNow.AddDays(-20), DateTime.UtcNow.AddDays(-1));
        fx.Subscription.StoreVaultedToken("cus_vault", "pm_vault");
        fx.Subscription.AssignDunningCampaign(fx.Campaign.Id);

        fx.Db.Products.Add(fx.Product);
        fx.Db.DunningCampaigns.Add(fx.Campaign);
        fx.Db.Subscriptions.Add(fx.Subscription);
        await fx.Db.SaveChangesAsync();
        return fx;
    }

    private static async Task<RecoveryFixture> SeedSuspendedAsync()
    {
        var fx = CreateFixture();
        fx.Subscription.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-10));
        fx.Subscription.MarkAsPastDue();
        fx.Subscription.Suspend();
        fx.Subscription.AssignDunningCampaign(fx.Campaign.Id);

        fx.Db.Products.Add(fx.Product);
        fx.Db.DunningCampaigns.Add(fx.Campaign);
        fx.Db.Subscriptions.Add(fx.Subscription);
        await fx.Db.SaveChangesAsync();
        return fx;
    }

    private static RecoveryFixture CreateFixture()
    {
        var orgId = Guid.CreateVersion7();
        var options = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var executionContext = Substitute.For<IExecutionContextAccessor>();
        executionContext.TenantId.Returns(Guid.Empty);

        var db = new CommerceDbContext(
            options,
            executionContext,
            Substitute.For<IMediator>(),
            new DatabaseJobTrigger());

        var product = new Product(
            orgId,
            "Pro Plan",
            "pro-plan",
            49.90m,
            "FIXED",
            0m,
            "MYR",
            "mo",
            "STRIPE",
            new CheckoutConfiguration(false, false, false),
            new[] { "telegram" });

        var campaign = new DunningCampaign(orgId, "Default recovery", "SUSPEND", 7);
        var clientId = Guid.CreateVersion7();
        var subscription = new Subscription(orgId, clientId, product.Id);

        var repository = Substitute.For<ICommerceRepository>();
        repository.When(r => r.AddSubscription(Arg.Any<Subscription>()))
            .Do(ci => db.Subscriptions.Add(ci.Arg<Subscription>()));
        repository.When(r => r.AddOrder(Arg.Any<Modules.Commerce.Domain.Aggregates.Order>()))
            .Do(ci => db.Orders.Add(ci.Arg<Modules.Commerce.Domain.Aggregates.Order>()));
        repository.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => db.SaveChangesAsync(callInfo.Arg<CancellationToken>()));

        var eventBus = Substitute.For<IEventBus>();
        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(clientId).Returns(new ClientProfileDto
        {
            Id = clientId.ToString(),
            Full_name = "Past Due User",
            Email = "pastdue@example.com"
        });

        var handler = new GatewayPaymentCompletedIntegrationEventHandler(repository, eventBus, crm, db);

        return new RecoveryFixture(orgId, db, product, campaign, subscription, handler, eventBus, crm);
    }

    private static GatewayPaymentCompletedIntegrationEvent PaymentEvent(
        RecoveryFixture fx,
        decimal amount,
        Dictionary<string, string> metadata) =>
        new(
            OrganizationId: fx.OrgId,
            GatewayTransactionId: "pi_recovery",
            AmountPaid: amount,
            Currency: "MYR",
            GatewayFee: 1m,
            TaxAmount: 0m,
            NetAmount: amount - 1m,
            FxRate: 1m,
            BaseCurrency: "MYR",
            LineItems: new List<LineItemDto>(),
            Metadata: metadata);

    private static async Task AssertRecoveredAsync(RecoveryFixture fx, decimal expectedRevenue, int expectedSaved)
    {
        var sub = await ReloadSubAsync(fx);
        sub.Status.Should().Be("ACTIVE");
        sub.CurrentDunningCampaignId.Should().BeNull();
        var campaign = await ReloadCampaignAsync(fx);
        campaign.RecoveredRevenue.Should().Be(expectedRevenue);
        campaign.SavedSubscriptions.Should().Be(expectedSaved);
    }

    private static Task<Subscription> ReloadSubAsync(RecoveryFixture fx) =>
        fx.Db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == fx.Subscription.Id);

    private static Task<DunningCampaign> ReloadCampaignAsync(RecoveryFixture fx) =>
        fx.Db.DunningCampaigns.IgnoreQueryFilters().SingleAsync(c => c.Id == fx.Campaign.Id);

    private sealed class RecoveryFixture : IDisposable
    {
        public RecoveryFixture(
            Guid orgId,
            CommerceDbContext db,
            Product product,
            DunningCampaign campaign,
            Subscription subscription,
            GatewayPaymentCompletedIntegrationEventHandler handler,
            IEventBus eventBus,
            ICrmQueryService crm)
        {
            OrgId = orgId;
            Db = db;
            Product = product;
            Campaign = campaign;
            Subscription = subscription;
            Handler = handler;
            EventBus = eventBus;
            Crm = crm;
        }

        public Guid OrgId { get; }
        public CommerceDbContext Db { get; }
        public Product Product { get; }
        public DunningCampaign Campaign { get; }
        public Subscription Subscription { get; }
        public GatewayPaymentCompletedIntegrationEventHandler Handler { get; }
        public IEventBus EventBus { get; }
        public ICrmQueryService Crm { get; }

        public void Dispose() => Db.Dispose();
    }
}
