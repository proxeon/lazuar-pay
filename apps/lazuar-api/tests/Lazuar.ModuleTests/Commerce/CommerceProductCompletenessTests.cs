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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modules.Commerce.Application;
using Modules.Commerce.Application.Commands;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Contracts.Events;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.Entities;
using Modules.Commerce.Domain.ValueObjects;
using Modules.Commerce.Infrastructure;
using Modules.Commerce.Infrastructure.EventHandlers;
using Modules.Commerce.Infrastructure.Repositories;
using Modules.Commerce.Infrastructure.Workers;
using Modules.Communications.Contracts;
using Modules.CRM.Contracts;
using Modules.One.Contracts;
using Modules.Payments.Contracts.Events;
using Modules.Payments.Contracts.Queries;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class CommerceProductCompletenessTests
{
    private static CommerceDbContext CreateDb(out Guid orgId)
    {
        orgId = Guid.CreateVersion7();
        var options = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var executionContext = Substitute.For<IExecutionContextAccessor>();
        executionContext.TenantId.Returns(Guid.Empty);

        return new CommerceDbContext(
            options,
            executionContext,
            Substitute.For<IMediator>(),
            new DatabaseJobTrigger());
    }

    private static Product CreateProduct(
        Guid orgId,
        string interval = "mo",
        bool requiresPhone = false,
        bool requiresAddress = false,
        bool requiresTaxId = false,
        string gatewayName = "STRIPE",
        string pricingModel = "FIXED")
    {
        return new Product(
            orgId,
            "Pro Plan",
            "pro-plan",
            100m,
            pricingModel,
            0m,
            "MYR",
            interval,
            gatewayName,
            new CheckoutConfiguration(requiresAddress, requiresTaxId, requiresPhone),
            new[] { "telegram" });
    }

    [Test]
    public async Task GatewayPaymentCompleted_ConfirmsCouponReservation_OnPaidCheckout()
    {
        using var db = CreateDb(out var orgId);
        var product = CreateProduct(orgId);
        var coupon = new Coupon(orgId, "SAVE10", "PERCENTAGE", 10m, maxUses: 10, expiresAt: null);
        coupon.Reserve();
        coupon.ReservedCount.Should().Be(1);
        coupon.UsedCount.Should().Be(0);

        var clientId = Guid.CreateVersion7();
        var session = new CheckoutSession(orgId, clientId, product.Id, coupon.Id, DateTime.UtcNow.AddHours(1));

        db.Products.Add(product);
        db.Coupons.Add(coupon);
        db.CheckoutSessions.Add(session);
        await db.SaveChangesAsync();

        var repository = Substitute.For<ICommerceRepository>();
        repository.When(r => r.AddSubscription(Arg.Any<Subscription>()))
            .Do(ci => db.Subscriptions.Add(ci.Arg<Subscription>()));
        repository.When(r => r.AddOrder(Arg.Any<Order>()))
            .Do(ci => db.Orders.Add(ci.Arg<Order>()));
        repository.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => db.SaveChangesAsync(callInfo.Arg<CancellationToken>()));

        var eventBus = Substitute.For<IEventBus>();
        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(Arg.Any<Guid>(), clientId).Returns(new ClientProfileDto
        {
            Id = clientId.ToString(),
            Full_name = "Test User",
            Email = "test@example.com"
        });

        var handler = new GatewayPaymentCompletedIntegrationEventHandler(repository, eventBus, crm, db);

        await handler.HandleAsync(new GatewayPaymentCompletedIntegrationEvent(
            OrganizationId: orgId,
            GatewayTransactionId: "pi_test_1",
            AmountPaid: 90m,
            Currency: "MYR",
            GatewayFee: 1m,
            TaxAmount: 0m,
            NetAmount: 89m,
            FxRate: 1m,
            BaseCurrency: "MYR",
            LineItems: new List<LineItemDto>(),
            Metadata: new Dictionary<string, string>
            {
                ["type"] = "commerce_subscription",
                ["subscription_id"] = session.Id.ToString(),
                ["tenant_id"] = orgId.ToString()
            }));

        var reloadedCoupon = await db.Coupons.IgnoreQueryFilters().FirstAsync(c => c.Id == coupon.Id);
        reloadedCoupon.ReservedCount.Should().Be(0);
        reloadedCoupon.UsedCount.Should().Be(1);

        var reloadedSession = await db.CheckoutSessions.IgnoreQueryFilters().FirstAsync(s => s.Id == session.Id);
        reloadedSession.Status.Should().Be("COMPLETED");
    }

    [Test]
    public async Task GatewayPaymentCompleted_ExpiredSession_RevivesAndFulfills()
    {
        using var db = CreateDb(out var orgId);
        var product = CreateProduct(orgId);
        var coupon = new Coupon(orgId, "SAVE10", "PERCENTAGE", 10m, maxUses: 10, expiresAt: null);
        coupon.Reserve();
        coupon.ReleaseReservation();

        var clientId = Guid.CreateVersion7();
        var session = new CheckoutSession(orgId, clientId, product.Id, coupon.Id, DateTime.UtcNow.AddHours(-2));
        session.Expire();

        db.Products.Add(product);
        db.Coupons.Add(coupon);
        db.CheckoutSessions.Add(session);
        await db.SaveChangesAsync();

        var subscriptions = new List<Subscription>();
        var repository = Substitute.For<ICommerceRepository>();
        repository.When(r => r.AddSubscription(Arg.Any<Subscription>()))
            .Do(ci =>
            {
                var sub = ci.Arg<Subscription>();
                subscriptions.Add(sub);
                db.Subscriptions.Add(sub);
            });
        repository.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => db.SaveChangesAsync(callInfo.Arg<CancellationToken>()));

        var eventBus = Substitute.For<IEventBus>();
        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(Arg.Any<Guid>(), clientId).Returns(new ClientProfileDto
        {
            Id = clientId.ToString(),
            Full_name = "Late Payer",
            Email = "late@example.com"
        });

        var handler = new GatewayPaymentCompletedIntegrationEventHandler(repository, eventBus, crm, db);
        await handler.HandleAsync(new GatewayPaymentCompletedIntegrationEvent(
            OrganizationId: orgId,
            GatewayTransactionId: "pi_late_1",
            AmountPaid: 90m,
            Currency: "MYR",
            GatewayFee: 1m,
            TaxAmount: 0m,
            NetAmount: 89m,
            FxRate: 1m,
            BaseCurrency: "MYR",
            LineItems: new List<LineItemDto>(),
            Metadata: new Dictionary<string, string>
            {
                ["type"] = "commerce_subscription",
                ["subscription_id"] = session.Id.ToString(),
                ["tenant_id"] = orgId.ToString()
            }));

        subscriptions.Should().HaveCount(1);
        var reloadedSession = await db.CheckoutSessions.IgnoreQueryFilters().FirstAsync(s => s.Id == session.Id);
        reloadedSession.Status.Should().Be("COMPLETED");
        var reloadedCoupon = await db.Coupons.IgnoreQueryFilters().FirstAsync(c => c.Id == coupon.Id);
        reloadedCoupon.UsedCount.Should().Be(1);
        reloadedCoupon.ReservedCount.Should().Be(0);
        await eventBus.Received().PublishAsync(Arg.Any<SubscriptionActivatedIntegrationEvent>());
    }

    [Test]
    public async Task CheckoutSessionExpiryJob_ExpiresOpenSessions_AndReleasesCouponReservation()
    {
        using var db = CreateDb(out var orgId);
        var product = CreateProduct(orgId);
        var coupon = new Coupon(orgId, "SAVE20", "FIXED", 20m, maxUses: 5, expiresAt: null);
        coupon.Reserve();

        var clientId = Guid.CreateVersion7();
        var expiredSession = new CheckoutSession(orgId, clientId, product.Id, coupon.Id, DateTime.UtcNow.AddHours(-2));
        var openSession = new CheckoutSession(orgId, clientId, product.Id, null, DateTime.UtcNow.AddHours(2));

        db.Products.Add(product);
        db.Coupons.Add(coupon);
        db.CheckoutSessions.Add(expiredSession);
        db.CheckoutSessions.Add(openSession);
        await db.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddSingleton(db);
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var job = new CheckoutSessionExpiryJob(scopeFactory, Substitute.For<ILogger<CheckoutSessionExpiryJob>>());
        await job.ExpireSessionsAsync(CancellationToken.None);

        var expired = await db.CheckoutSessions.IgnoreQueryFilters().FirstAsync(s => s.Id == expiredSession.Id);
        expired.Status.Should().Be("EXPIRED");

        var stillOpen = await db.CheckoutSessions.IgnoreQueryFilters().FirstAsync(s => s.Id == openSession.Id);
        stillOpen.Status.Should().Be("OPEN");

        var reloadedCoupon = await db.Coupons.IgnoreQueryFilters().FirstAsync(c => c.Id == coupon.Id);
        reloadedCoupon.ReservedCount.Should().Be(0);
        reloadedCoupon.UsedCount.Should().Be(0);
    }

    [Test]
    public async Task CancelAdminSubscription_SetsCanceledAndPublishesEvent()
    {
        var orgId = Guid.CreateVersion7();
        var product = CreateProduct(orgId);
        var sub = new Subscription(orgId, Guid.CreateVersion7(), product.Id);
        sub.Activate(DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));

        var repository = Substitute.For<ICommerceRepository>();
        repository.GetSubscriptionByIdAsync(Arg.Any<Guid>(), sub.Id, Arg.Any<CancellationToken>()).Returns(sub);
        repository.GetProductByIdAsync(Arg.Any<Guid>(), product.Id, Arg.Any<CancellationToken>()).Returns(product);

        var eventBus = Substitute.For<IEventBus>();
        var handler = new CancelAdminSubscriptionCommandHandler(repository, eventBus);

        await handler.Handle(new CancelAdminSubscriptionCommand(orgId, sub.Id), CancellationToken.None);

        sub.Status.Should().Be("CANCELED");
        await eventBus.Received(1).PublishAsync(Arg.Any<SubscriptionCanceledIntegrationEvent>());
        await repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task MarkCheckoutAsPaidOffline_ProductSession_CreatesActiveSubscription_AndTxLog()
    {
        var orgId = Guid.CreateVersion7();
        var product = CreateProduct(orgId, interval: "mo");
        var clientId = Guid.CreateVersion7();
        var session = new CheckoutSession(orgId, clientId, product.Id, couponId: null, DateTime.UtcNow.AddHours(1));

        var subscriptions = new List<Subscription>();
        var logs = new List<CommerceTransactionLog>();

        var repository = Substitute.For<ICommerceRepository>();
        repository.GetCheckoutSessionByIdAsync(Arg.Any<Guid>(), session.Id, Arg.Any<CancellationToken>()).Returns(session);
        repository.GetProductByIdAsync(Arg.Any<Guid>(), product.Id, Arg.Any<CancellationToken>()).Returns(product);
        repository.When(r => r.AddSubscription(Arg.Any<Subscription>())).Do(ci => subscriptions.Add(ci.Arg<Subscription>()));
        repository.When(r => r.AddTransactionLog(Arg.Any<CommerceTransactionLog>())).Do(ci => logs.Add(ci.Arg<CommerceTransactionLog>()));

        var eventBus = Substitute.For<IEventBus>();
        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(Arg.Any<Guid>(), clientId).Returns(new ClientProfileDto
        {
            Id = clientId.ToString(),
            Full_name = "Offline Buyer",
            Email = "offline@example.com"
        });

        var handler = CreateMarkPaidHandler(repository, eventBus, crm);
        await handler.Handle(new MarkCheckoutAsPaidOfflineCommand(orgId, session.Id), CancellationToken.None);

        session.Status.Should().Be("COMPLETED");
        subscriptions.Should().HaveCount(1);
        subscriptions[0].Status.Should().Be("ACTIVE");
        subscriptions[0].IsReminderOnly.Should().BeTrue();
        logs.Should().HaveCount(1);
        logs[0].Status.Should().Be("CONFIRMED");
        logs[0].RecordedByName.Should().Be("MANUAL_OFFLINE");

        await eventBus.Received().PublishAsync(Arg.Any<SubscriptionActivatedIntegrationEvent>());
        await eventBus.Received().PublishAsync(Arg.Any<ManualSubscriberEnrolledIntegrationEvent>());
    }

    [Test]
    public async Task MarkCheckoutAsPaidOffline_ProductRequiresTaxId_PublishesB2b()
    {
        var orgId = Guid.CreateVersion7();
        var product = CreateProduct(orgId, requiresTaxId: true);
        var clientId = Guid.CreateVersion7();
        var session = new CheckoutSession(orgId, clientId, product.Id, couponId: null, DateTime.UtcNow.AddHours(1));

        var repository = Substitute.For<ICommerceRepository>();
        repository.GetCheckoutSessionByIdAsync(Arg.Any<Guid>(), session.Id, Arg.Any<CancellationToken>()).Returns(session);
        repository.GetProductByIdAsync(Arg.Any<Guid>(), product.Id, Arg.Any<CancellationToken>()).Returns(product);

        var eventBus = Substitute.For<IEventBus>();
        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(Arg.Any<Guid>(), clientId).Returns(new ClientProfileDto
        {
            Id = clientId.ToString(),
            Full_name = "Offline Buyer",
            Email = "offline@example.com"
        });

        var handler = CreateMarkPaidHandler(repository, eventBus, crm);
        await handler.Handle(new MarkCheckoutAsPaidOfflineCommand(orgId, session.Id), CancellationToken.None);

        await eventBus.Received().PublishAsync(Arg.Is<ManualSubscriberEnrolledIntegrationEvent>(e =>
            e.IsB2bRequired));
    }

    [Test]
    public async Task MarkCheckoutAsPaidOffline_CustomSession_CompletesWithoutSubscription()
    {
        var orgId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var session = new CheckoutSession(
            orgId,
            clientId,
            new[] { new AdHocLineItem("Consulting", 1, 250m) },
            DateTime.UtcNow.AddDays(1),
            isB2bRequired: false);

        var subscriptions = new List<Subscription>();
        var logs = new List<CommerceTransactionLog>();

        var repository = Substitute.For<ICommerceRepository>();
        repository.GetCheckoutSessionByIdAsync(Arg.Any<Guid>(), session.Id, Arg.Any<CancellationToken>()).Returns(session);
        repository.When(r => r.AddSubscription(Arg.Any<Subscription>())).Do(ci => subscriptions.Add(ci.Arg<Subscription>()));
        repository.When(r => r.AddTransactionLog(Arg.Any<CommerceTransactionLog>())).Do(ci => logs.Add(ci.Arg<CommerceTransactionLog>()));

        var eventBus = Substitute.For<IEventBus>();
        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(Arg.Any<Guid>(), clientId).Returns(new ClientProfileDto
        {
            Id = clientId.ToString(),
            Full_name = "Custom Buyer",
            Email = "custom@example.com"
        });

        var handler = CreateMarkPaidHandler(repository, eventBus, crm);
        await handler.Handle(new MarkCheckoutAsPaidOfflineCommand(orgId, session.Id), CancellationToken.None);

        session.Status.Should().Be("COMPLETED");
        subscriptions.Should().BeEmpty();
        logs.Should().HaveCount(1);
        logs[0].Amount.Should().Be(250m);
        await eventBus.Received().PublishAsync(Arg.Any<ManualSubscriberEnrolledIntegrationEvent>());
        await eventBus.DidNotReceive().PublishAsync(Arg.Any<SubscriptionActivatedIntegrationEvent>());
    }

    [Test]
    public async Task InitiateCheckout_EnforcesRequiresPhone()
    {
        var orgId = Guid.CreateVersion7();
        var product = CreateProduct(orgId, requiresPhone: true);

        var repository = Substitute.For<ICommerceRepository>();
        repository.GetProductBySlugAsync(orgId, "pro-plan", Arg.Any<CancellationToken>()).Returns(product);

        var one = Substitute.For<Modules.One.Contracts.IOneQueryService>();
        one.GetTenantIdBySlugAsync("acme").Returns(orgId);

        var comms = Substitute.For<Modules.Communications.Contracts.ICommunicationsQueryService>();
        comms.HasValidEmailConfigAsync(orgId).Returns(true);

        var mediator = Substitute.For<IMediator>();
        var config = Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>();

        // Issue 167: phone check is first, but still compose billing so a later
        // SST-before-phone reorder would not change this fixture's exception.
        var handler = new InitiateCheckoutCommandHandler(
            one, repository, mediator, config, comms, CommerceBillingStubs.NoSstBilling());

        var act = async () => await handler.Handle(new InitiateCheckoutCommand(
            "acme",
            "pro-plan",
            "Ada",
            "ada@example.com",
            Phone: null,
            TaxId: null,
            IdType: null,
            IdValue: null,
            CompanyName: null,
            AddressLine1: null,
            City: null,
            PostalCode: null,
            StateCode: null,
            CountryCode: null,
            Quantity: 1,
            IsGuestCheckout: true,
            CouponCode: null), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*phone*");
    }

    [Test]
    public async Task RecordSubscriberPayment_FromPastDue_RecoversAndLogsManualTx()
    {
        var orgId = Guid.CreateVersion7();
        var product = CreateProduct(orgId);
        var clientId = Guid.CreateVersion7();
        var campaign = new DunningCampaign(orgId, "Default recovery", "SUSPEND", 7);
        var sub = new Subscription(orgId, clientId, product.Id);
        sub.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-10));
        sub.MarkAsPastDue();
        sub.AssignDunningCampaign(campaign.Id);

        var logs = new List<CommerceTransactionLog>();
        var repository = Substitute.For<ICommerceRepository>();
        repository.GetSubscriptionByIdAsync(Arg.Any<Guid>(), sub.Id, Arg.Any<CancellationToken>()).Returns(sub);
        repository.GetProductByIdAsync(Arg.Any<Guid>(), product.Id, Arg.Any<CancellationToken>()).Returns(product);
        repository.GetDunningCampaignByIdAsync(orgId, campaign.Id, Arg.Any<CancellationToken>()).Returns(campaign);
        repository.When(r => r.AddTransactionLog(Arg.Any<CommerceTransactionLog>())).Do(ci => logs.Add(ci.Arg<CommerceTransactionLog>()));

        var eventBus = Substitute.For<IEventBus>();
        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(Arg.Any<Guid>(), clientId).Returns(new ClientProfileDto
        {
            Id = clientId.ToString(),
            Full_name = "Past Due User",
            Email = "pastdue@example.com"
        });

        var handler = new RecordSubscriberPaymentCommandHandler(repository, eventBus, crm);
        await handler.Handle(new RecordSubscriberPaymentCommand(
            orgId, sub.Id, 100m, "BANK_TRANSFER", "TRX-1"), CancellationToken.None);

        sub.Status.Should().Be("ACTIVE");
        sub.CurrentDunningCampaignId.Should().BeNull();
        campaign.RecoveredRevenue.Should().Be(100m);
        campaign.SavedSubscriptions.Should().Be(1);
        logs.Should().HaveCount(1);
        logs[0].RecordedByName.Should().Be("BANK_TRANSFER");
        await eventBus.Received().PublishAsync(Arg.Any<ManualSubscriberEnrolledIntegrationEvent>());
        await eventBus.Received().PublishAsync(Arg.Any<SubscriptionActivatedIntegrationEvent>());
    }

    [Test]
    public async Task RecordSubscriberPayment_FromPastDue_Comped_DoesNotRecordRecovery()
    {
        var orgId = Guid.CreateVersion7();
        var product = CreateProduct(orgId);
        var clientId = Guid.CreateVersion7();
        var campaign = new DunningCampaign(orgId, "Default recovery", "SUSPEND", 7);
        var sub = new Subscription(orgId, clientId, product.Id);
        sub.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-10));
        sub.MarkAsPastDue();
        sub.AssignDunningCampaign(campaign.Id);

        var repository = Substitute.For<ICommerceRepository>();
        repository.GetSubscriptionByIdAsync(Arg.Any<Guid>(), sub.Id, Arg.Any<CancellationToken>()).Returns(sub);
        repository.GetProductByIdAsync(Arg.Any<Guid>(), product.Id, Arg.Any<CancellationToken>()).Returns(product);
        repository.GetDunningCampaignByIdAsync(orgId, campaign.Id, Arg.Any<CancellationToken>()).Returns(campaign);

        var handler = new RecordSubscriberPaymentCommandHandler(
            repository,
            Substitute.For<IEventBus>(),
            Substitute.For<ICrmQueryService>());
        await handler.Handle(new RecordSubscriberPaymentCommand(
            orgId, sub.Id, 100m, "COMPED", null), CancellationToken.None);

        sub.Status.Should().Be("ACTIVE");
        sub.CurrentDunningCampaignId.Should().BeNull();
        campaign.RecoveredRevenue.Should().Be(0);
        campaign.SavedSubscriptions.Should().Be(0);
        await repository.DidNotReceive().GetDunningCampaignByIdAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RecordSubscriberPayment_FromActive_DoesNotRecordRecovery()
    {
        var orgId = Guid.CreateVersion7();
        var product = CreateProduct(orgId);
        var clientId = Guid.CreateVersion7();
        var campaign = new DunningCampaign(orgId, "Stale assignment", "SUSPEND", 7);
        var sub = new Subscription(orgId, clientId, product.Id);
        sub.Activate(DateTime.UtcNow.AddDays(-20), DateTime.UtcNow.AddDays(-1));
        sub.AssignDunningCampaign(campaign.Id);

        var repository = Substitute.For<ICommerceRepository>();
        repository.GetSubscriptionByIdAsync(Arg.Any<Guid>(), sub.Id, Arg.Any<CancellationToken>()).Returns(sub);
        repository.GetProductByIdAsync(Arg.Any<Guid>(), product.Id, Arg.Any<CancellationToken>()).Returns(product);
        repository.GetDunningCampaignByIdAsync(orgId, campaign.Id, Arg.Any<CancellationToken>()).Returns(campaign);

        var handler = new RecordSubscriberPaymentCommandHandler(
            repository,
            Substitute.For<IEventBus>(),
            Substitute.For<ICrmQueryService>());
        await handler.Handle(new RecordSubscriberPaymentCommand(
            orgId, sub.Id, 100m, "BANK_TRANSFER", "TRX-ACTIVE"), CancellationToken.None);

        sub.Status.Should().Be("ACTIVE");
        campaign.RecoveredRevenue.Should().Be(0);
        campaign.SavedSubscriptions.Should().Be(0);
        await repository.DidNotReceive().GetDunningCampaignByIdAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task InitiateCheckout_HundredPercentCoupon_StripeMonthly_MintsHop2SetupSession()
    {
        var orgId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var product = CreateProduct(orgId);
        var coupon = new Coupon(orgId, "FREE100", "PERCENTAGE", 100m, maxUses: 10, expiresAt: null);

        CheckoutSession? session = null;
        GenerateCheckoutSessionQuery? payments = null;
        var repository = Substitute.For<ICommerceRepository>();
        repository.GetProductBySlugAsync(orgId, "pro-plan", Arg.Any<CancellationToken>()).Returns(product);
        repository.GetCouponByCodeWithLockAsync(orgId, "FREE100", Arg.Any<CancellationToken>()).Returns(coupon);
        repository.When(r => r.AddCheckoutSession(Arg.Any<CheckoutSession>()))
            .Do(ci => session = ci.Arg<CheckoutSession>());

        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ResolveClientProfileCommand>(), Arg.Any<CancellationToken>()).Returns(clientId);
        mediator.Send(Arg.Do<GenerateCheckoutSessionQuery>(q => payments = q), Arg.Any<CancellationToken>())
            .Returns("https://checkout.stripe.test/cs_setup");

        var handler = CreateInitiateHandler(orgId, repository, mediator);
        var result = await handler.Handle(GuestCheckoutCommand("FREE100"), CancellationToken.None);

        session.Should().NotBeNull();
        session!.Status.Should().Be("OPEN");
        result.IsZeroAmountBypass.Should().BeFalse();
        result.Url.Should().Be("https://checkout.stripe.test/cs_setup");

        payments.Should().NotBeNull();
        payments!.Amount.Should().Be(0m);
        payments.SetupFutureUsage.Should().BeTrue();
        payments.Metadata.Should().ContainKey("type");
        payments.Metadata["type"].Should().Be("commerce_subscription");

        await mediator.DidNotReceive().Send(
            Arg.Any<ProcessZeroAmountCheckoutCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task InitiateCheckout_TrialStripeMonthly_MintsHop2WithCommerceType()
    {
        var orgId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var product = CreateProduct(orgId);
        product.SetTrialDays(14);

        CheckoutSession? session = null;
        GenerateCheckoutSessionQuery? payments = null;
        var repository = Substitute.For<ICommerceRepository>();
        repository.GetProductBySlugAsync(orgId, "pro-plan", Arg.Any<CancellationToken>()).Returns(product);
        repository.When(r => r.AddCheckoutSession(Arg.Any<CheckoutSession>()))
            .Do(ci => session = ci.Arg<CheckoutSession>());

        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ResolveClientProfileCommand>(), Arg.Any<CancellationToken>()).Returns(clientId);
        mediator.Send(Arg.Do<GenerateCheckoutSessionQuery>(q => payments = q), Arg.Any<CancellationToken>())
            .Returns("https://checkout.stripe.test/cs_trial");

        var handler = CreateInitiateHandler(orgId, repository, mediator);
        var result = await handler.Handle(GuestCheckoutCommand(couponCode: null), CancellationToken.None);

        session.Should().NotBeNull();
        session!.Status.Should().Be("OPEN");
        result.IsZeroAmountBypass.Should().BeFalse();
        result.Url.Should().Be("https://checkout.stripe.test/cs_trial");

        payments.Should().NotBeNull();
        payments!.Amount.Should().Be(0m);
        payments.SetupFutureUsage.Should().BeTrue();
        payments.Metadata["type"].Should().Be("commerce_subscription");
        payments.Metadata.Should().NotContainValue("trial");

        await mediator.DidNotReceive().Send(
            Arg.Any<ProcessZeroAmountCheckoutCommand>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GatewayPaymentCompleted_TrialProduct_CommerceType_ActivatesTrialingWithVault()
    {
        using var db = CreateDb(out var orgId);
        var product = CreateProduct(orgId);
        product.SetTrialDays(14);
        var clientId = Guid.CreateVersion7();
        var session = new CheckoutSession(orgId, clientId, product.Id, couponId: null, DateTime.UtcNow.AddHours(1));

        db.Products.Add(product);
        db.CheckoutSessions.Add(session);
        await db.SaveChangesAsync();

        var handler = CreateOpenCheckoutPaymentHandler(db, clientId);
        await handler.HandleAsync(CreateCommercePaymentCompleted(
            orgId, session.Id, customerId: "cus_trial", tokenId: "pm_trial", amountPaid: 0m));

        var reloaded = await db.CheckoutSessions.IgnoreQueryFilters().FirstAsync(s => s.Id == session.Id);
        reloaded.Status.Should().Be("COMPLETED");

        var sub = await db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.ClientProfileId == clientId);
        sub.Status.Should().Be("TRIALING");
        sub.IsReminderOnly.Should().BeFalse();
        sub.VaultedCustomerId.Should().Be("cus_trial");
        sub.VaultedTokenId.Should().Be("pm_trial");
    }

    [Test]
    public async Task GatewayPaymentCompleted_LegacyTypeTrial_ActivatesTrialingWithVault()
    {
        using var db = CreateDb(out var orgId);
        var product = CreateProduct(orgId);
        product.SetTrialDays(14);
        var clientId = Guid.CreateVersion7();
        var session = new CheckoutSession(orgId, clientId, product.Id, couponId: null, DateTime.UtcNow.AddHours(1));

        db.Products.Add(product);
        db.CheckoutSessions.Add(session);
        await db.SaveChangesAsync();

        var handler = CreateOpenCheckoutPaymentHandler(db, clientId);
        var ev = CreateCommercePaymentCompleted(
            orgId, session.Id, customerId: "cus_trial", tokenId: "pm_trial", amountPaid: 0m);
        ev.Metadata["type"] = "trial";

        await handler.HandleAsync(ev);

        var reloaded = await db.CheckoutSessions.IgnoreQueryFilters().FirstAsync(s => s.Id == session.Id);
        reloaded.Status.Should().Be("COMPLETED");
        var sub = await db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.ClientProfileId == clientId);
        sub.Status.Should().Be("TRIALING");
        sub.VaultedTokenId.Should().Be("pm_trial");
    }

    [Test]
    public async Task InitiateCheckout_HundredPercentCoupon_BillplzMonthly_StillBypasses()
    {
        var orgId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var product = CreateProduct(orgId, gatewayName: "BILLPLZ");
        var coupon = new Coupon(orgId, "FREE100", "PERCENTAGE", 100m, maxUses: 10, expiresAt: null);

        CheckoutSession? session = null;
        var repository = Substitute.For<ICommerceRepository>();
        repository.GetProductBySlugAsync(orgId, "pro-plan", Arg.Any<CancellationToken>()).Returns(product);
        repository.GetCouponByCodeWithLockAsync(orgId, "FREE100", Arg.Any<CancellationToken>()).Returns(coupon);
        repository.When(r => r.AddCheckoutSession(Arg.Any<CheckoutSession>()))
            .Do(ci => session = ci.Arg<CheckoutSession>());
        // GetCheckoutSessionByIdAsync(organizationId, sessionId) — two Guid args.
        // Arg<Guid>() is ambiguous (same NSubstitute pitfall as issue 165).
        repository.GetCheckoutSessionByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => session != null && session.Id == ci.ArgAt<Guid>(1) ? session : null);
        repository.GetProductByIdAsync(Arg.Any<Guid>(), product.Id, Arg.Any<CancellationToken>()).Returns(product);
        repository.GetCouponByIdAsync(Arg.Any<Guid>(), coupon.Id, Arg.Any<CancellationToken>()).Returns(coupon);

        var eventBus = Substitute.For<IEventBus>();
        var zeroHandler = new ProcessZeroAmountCheckoutCommandHandler(repository, eventBus);

        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ResolveClientProfileCommand>(), Arg.Any<CancellationToken>()).Returns(clientId);
        mediator.Send(Arg.Any<ProcessZeroAmountCheckoutCommand>(), Arg.Any<CancellationToken>())
            .Returns(ci => zeroHandler.Handle(
                ci.Arg<ProcessZeroAmountCheckoutCommand>(),
                ci.Arg<CancellationToken>()));

        var handler = CreateInitiateHandler(orgId, repository, mediator);
        var result = await handler.Handle(GuestCheckoutCommand("FREE100"), CancellationToken.None);

        session.Should().NotBeNull();
        result.IsZeroAmountBypass.Should().BeTrue();
        result.Url.Should().Be($"https://portal.test/acme/checkout/pro-plan/success?sub_id={session!.Id}");
        session.Status.Should().Be("COMPLETED");
        await mediator.DidNotReceive().Send(
            Arg.Any<GenerateCheckoutSessionQuery>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task InitiateCheckout_ZeroAmountCoupon_ReturnsSuccessUrlWithSessionId_AndCompletesSession()
    {
        var orgId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var product = CreateProduct(orgId, interval: "one_time");
        var coupon = new Coupon(orgId, "FREE100", "PERCENTAGE", 100m, maxUses: 10, expiresAt: null);

        CheckoutSession? session = null;
        var repository = Substitute.For<ICommerceRepository>();
        repository.GetProductBySlugAsync(orgId, "pro-plan", Arg.Any<CancellationToken>()).Returns(product);
        repository.GetCouponByCodeWithLockAsync(orgId, "FREE100", Arg.Any<CancellationToken>()).Returns(coupon);
        repository.When(r => r.AddCheckoutSession(Arg.Any<CheckoutSession>()))
            .Do(ci => session = ci.Arg<CheckoutSession>());
        // GetCheckoutSessionByIdAsync(organizationId, sessionId) — two Guid args.
        // Arg<Guid>() is ambiguous (same NSubstitute pitfall as issue 165).
        repository.GetCheckoutSessionByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => session != null && session.Id == ci.ArgAt<Guid>(1) ? session : null);
        repository.GetProductByIdAsync(Arg.Any<Guid>(), product.Id, Arg.Any<CancellationToken>()).Returns(product);
        repository.GetCouponByIdAsync(Arg.Any<Guid>(), coupon.Id, Arg.Any<CancellationToken>()).Returns(coupon);

        var one = Substitute.For<IOneQueryService>();
        one.GetTenantIdBySlugAsync("acme").Returns(orgId);

        var comms = Substitute.For<ICommunicationsQueryService>();
        comms.HasValidEmailConfigAsync(orgId).Returns(true);

        var eventBus = Substitute.For<IEventBus>();
        var zeroHandler = new ProcessZeroAmountCheckoutCommandHandler(repository, eventBus);

        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ResolveClientProfileCommand>(), Arg.Any<CancellationToken>()).Returns(clientId);
        mediator.Send(Arg.Any<ProcessZeroAmountCheckoutCommand>(), Arg.Any<CancellationToken>())
            .Returns(ci => zeroHandler.Handle(
                ci.Arg<ProcessZeroAmountCheckoutCommand>(),
                ci.Arg<CancellationToken>()));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:ClientUrl"] = "https://portal.test"
            })
            .Build();

        var handler = new InitiateCheckoutCommandHandler(
            one, repository, mediator, config, comms, CommerceBillingStubs.NoSstBilling());
        var result = await handler.Handle(GuestCheckoutCommand("FREE100"), CancellationToken.None);

        session.Should().NotBeNull();
        result.IsZeroAmountBypass.Should().BeTrue();
        result.Url.Should().Be($"https://portal.test/acme/checkout/pro-plan/success?sub_id={session!.Id}");
        session.Status.Should().Be("COMPLETED");
    }

    [Test]
    public async Task InitiateCheckout_PaidPath_KeepsSessionOpen_AndReturnsGatewayUrl()
    {
        var orgId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var product = CreateProduct(orgId);

        CheckoutSession? session = null;
        var repository = Substitute.For<ICommerceRepository>();
        repository.GetProductBySlugAsync(orgId, "pro-plan", Arg.Any<CancellationToken>()).Returns(product);
        repository.When(r => r.AddCheckoutSession(Arg.Any<CheckoutSession>()))
            .Do(ci => session = ci.Arg<CheckoutSession>());

        var one = Substitute.For<IOneQueryService>();
        one.GetTenantIdBySlugAsync("acme").Returns(orgId);

        var comms = Substitute.For<ICommunicationsQueryService>();
        comms.HasValidEmailConfigAsync(orgId).Returns(true);

        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ResolveClientProfileCommand>(), Arg.Any<CancellationToken>()).Returns(clientId);
        mediator.Send(Arg.Any<GenerateCheckoutSessionQuery>(), Arg.Any<CancellationToken>())
            .Returns("https://gateway.test/pay/xyz");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:ClientUrl"] = "https://portal.test"
            })
            .Build();

        var handler = new InitiateCheckoutCommandHandler(
            one, repository, mediator, config, comms, CommerceBillingStubs.NoSstBilling());
        var result = await handler.Handle(GuestCheckoutCommand(couponCode: null), CancellationToken.None);

        session.Should().NotBeNull();
        result.IsZeroAmountBypass.Should().BeFalse();
        result.Url.Should().Be("https://gateway.test/pay/xyz");
        session!.Status.Should().Be("OPEN");
    }

    [Test]
    public async Task GatewayPaymentCompleted_SameEventTwice_DoesNotCreateSecondSubscription()
    {
        using var db = CreateDb(out var orgId);
        var product = CreateProduct(orgId);
        var clientId = Guid.CreateVersion7();
        var session = new CheckoutSession(orgId, clientId, product.Id, couponId: null, DateTime.UtcNow.AddHours(1));

        db.Products.Add(product);
        db.CheckoutSessions.Add(session);
        await db.SaveChangesAsync();

        var handler = CreateOpenCheckoutPaymentHandler(db, clientId);
        var @event = CreateCommercePaymentCompleted(orgId, session.Id);

        await handler.HandleAsync(@event);
        await handler.HandleAsync(@event);

        var reloaded = await db.CheckoutSessions.IgnoreQueryFilters().FirstAsync(s => s.Id == session.Id);
        reloaded.Status.Should().Be("COMPLETED");
        (await db.Subscriptions.IgnoreQueryFilters().CountAsync()).Should().Be(1);
        (await db.Orders.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task GatewayPaymentCompleted_NonCommerceType_LeavesSessionOpen()
    {
        using var db = CreateDb(out var orgId);
        var product = CreateProduct(orgId);
        var clientId = Guid.CreateVersion7();
        var session = new CheckoutSession(orgId, clientId, product.Id, couponId: null, DateTime.UtcNow.AddHours(1));

        db.Products.Add(product);
        db.CheckoutSessions.Add(session);
        await db.SaveChangesAsync();

        var handler = CreateOpenCheckoutPaymentHandler(db, clientId);
        await handler.HandleAsync(new GatewayPaymentCompletedIntegrationEvent(
            OrganizationId: orgId,
            GatewayTransactionId: "pi_other",
            AmountPaid: 100m,
            Currency: "MYR",
            GatewayFee: 1m,
            TaxAmount: 0m,
            NetAmount: 99m,
            FxRate: 1m,
            BaseCurrency: "MYR",
            LineItems: new List<LineItemDto>(),
            Metadata: new Dictionary<string, string>
            {
                ["type"] = "utility_credit_topup",
                ["subscription_id"] = session.Id.ToString(),
                ["tenant_id"] = orgId.ToString()
            }));

        var reloaded = await db.CheckoutSessions.IgnoreQueryFilters().FirstAsync(s => s.Id == session.Id);
        reloaded.Status.Should().Be("OPEN");
        (await db.Subscriptions.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await db.Orders.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task GatewayPaymentCompleted_AlreadyCompletedSession_DoesNotCreateOrderOrSubscription()
    {
        using var db = CreateDb(out var orgId);
        var product = CreateProduct(orgId);
        var clientId = Guid.CreateVersion7();
        var session = new CheckoutSession(orgId, clientId, product.Id, couponId: null, DateTime.UtcNow.AddHours(1));
        session.Complete();

        db.Products.Add(product);
        db.CheckoutSessions.Add(session);
        await db.SaveChangesAsync();

        var handler = CreateOpenCheckoutPaymentHandler(db, clientId);
        await handler.HandleAsync(CreateCommercePaymentCompleted(orgId, session.Id));

        var reloaded = await db.CheckoutSessions.IgnoreQueryFilters().FirstAsync(s => s.Id == session.Id);
        reloaded.Status.Should().Be("COMPLETED");
        (await db.Subscriptions.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await db.Orders.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task OpenCheckout_Billplz_NoTokens_ActivatesReminderOnly()
    {
        using var db = CreateDb(out var orgId);
        var product = CreateProduct(orgId, gatewayName: "BILLPLZ");
        var clientId = Guid.CreateVersion7();
        var session = new CheckoutSession(orgId, clientId, product.Id, couponId: null, DateTime.UtcNow.AddHours(1));
        db.Products.Add(product);
        db.CheckoutSessions.Add(session);
        await db.SaveChangesAsync();

        var handler = CreateOpenCheckoutPaymentHandler(db, clientId);
        await handler.HandleAsync(CreateCommercePaymentCompleted(orgId, session.Id));

        var sub = await db.Subscriptions.IgnoreQueryFilters().SingleAsync();
        sub.IsReminderOnly.Should().BeTrue();
        sub.VaultedCustomerId.Should().BeNull();
        sub.VaultedTokenId.Should().BeNull();
        sub.Status.Should().Be("ACTIVE");
    }

    [Test]
    public async Task OpenCheckout_Billplz_JunkTokens_StillReminderOnly_DoesNotStoreVault()
    {
        using var db = CreateDb(out var orgId);
        var product = CreateProduct(orgId, gatewayName: "BILLPLZ");
        var clientId = Guid.CreateVersion7();
        var session = new CheckoutSession(orgId, clientId, product.Id, couponId: null, DateTime.UtcNow.AddHours(1));
        db.Products.Add(product);
        db.CheckoutSessions.Add(session);
        await db.SaveChangesAsync();

        var handler = CreateOpenCheckoutPaymentHandler(db, clientId);
        await handler.HandleAsync(CreateCommercePaymentCompleted(orgId, session.Id, "cus_junk", "tok_junk"));

        var sub = await db.Subscriptions.IgnoreQueryFilters().SingleAsync();
        sub.IsReminderOnly.Should().BeTrue();
        sub.VaultedTokenId.Should().BeNull();
    }

    [Test]
    public async Task OpenCheckout_Stripe_WithVaultIds_StoresVault_NotReminderOnly()
    {
        using var db = CreateDb(out var orgId);
        var product = CreateProduct(orgId, gatewayName: "STRIPE");
        var clientId = Guid.CreateVersion7();
        var session = new CheckoutSession(orgId, clientId, product.Id, couponId: null, DateTime.UtcNow.AddHours(1));
        db.Products.Add(product);
        db.CheckoutSessions.Add(session);
        await db.SaveChangesAsync();

        var handler = CreateOpenCheckoutPaymentHandler(db, clientId);
        await handler.HandleAsync(CreateCommercePaymentCompleted(orgId, session.Id, "cus_1", "pm_1"));

        var sub = await db.Subscriptions.IgnoreQueryFilters().SingleAsync();
        sub.IsReminderOnly.Should().BeFalse();
        sub.VaultedCustomerId.Should().Be("cus_1");
        sub.VaultedTokenId.Should().Be("pm_1");
    }

    [Test]
    public async Task OpenCheckout_Chip_TokenOnly_VaultsUsingTokenAsCustomer()
    {
        using var db = CreateDb(out var orgId);
        var product = CreateProduct(orgId, gatewayName: "CHIP");
        var clientId = Guid.CreateVersion7();
        var session = new CheckoutSession(orgId, clientId, product.Id, couponId: null, DateTime.UtcNow.AddHours(1));
        db.Products.Add(product);
        db.CheckoutSessions.Add(session);
        await db.SaveChangesAsync();

        var handler = CreateOpenCheckoutPaymentHandler(db, clientId);
        await handler.HandleAsync(CreateCommercePaymentCompleted(orgId, session.Id, customerId: null, tokenId: "purchase_abc"));

        var sub = await db.Subscriptions.IgnoreQueryFilters().SingleAsync();
        sub.IsReminderOnly.Should().BeFalse();
        sub.VaultedTokenId.Should().Be("purchase_abc");
        sub.VaultedCustomerId.Should().Be("purchase_abc");
    }

    [Test]
    public async Task ProcessZeroAmount_Recurring_ActivatesReminderOnly()
    {
        var orgId = Guid.CreateVersion7();
        var product = new Product(
            orgId,
            "Free Plan",
            "free-plan",
            0m,
            "FIXED",
            0m,
            "MYR",
            "mo",
            "STRIPE",
            new CheckoutConfiguration(false, false, false),
            new[] { "telegram" });
        var clientId = Guid.CreateVersion7();
        var session = new CheckoutSession(orgId, clientId, product.Id, couponId: null, DateTime.UtcNow.AddHours(1));

        Subscription? created = null;
        var repository = Substitute.For<ICommerceRepository>();
        repository.GetCheckoutSessionByIdAsync(Arg.Any<Guid>(), session.Id, Arg.Any<CancellationToken>()).Returns(session);
        repository.GetProductByIdAsync(Arg.Any<Guid>(), product.Id, Arg.Any<CancellationToken>()).Returns(product);
        repository.When(r => r.AddSubscription(Arg.Any<Subscription>()))
            .Do(ci => created = ci.Arg<Subscription>());

        var handler = new ProcessZeroAmountCheckoutCommandHandler(repository, Substitute.For<IEventBus>());
        await handler.Handle(new ProcessZeroAmountCheckoutCommand(orgId, session.Id), CancellationToken.None);

        created.Should().NotBeNull();
        created!.IsReminderOnly.Should().BeTrue();
        created.Status.Should().Be("ACTIVE");
        created.VaultedTokenId.Should().BeNull();
    }

    [Test]
    public async Task ProcessZeroAmount_BillplzTrial_PublishesMatchingDiscount()
    {
        var orgId = Guid.CreateVersion7();
        var product = CreateProduct(orgId, gatewayName: "BILLPLZ");
        product.SetTrialDays(14);
        var clientId = Guid.CreateVersion7();
        var session = new CheckoutSession(orgId, clientId, product.Id, couponId: null, DateTime.UtcNow.AddHours(1));

        var repository = Substitute.For<ICommerceRepository>();
        repository.GetCheckoutSessionByIdAsync(Arg.Any<Guid>(), session.Id, Arg.Any<CancellationToken>()).Returns(session);
        repository.GetProductByIdAsync(Arg.Any<Guid>(), product.Id, Arg.Any<CancellationToken>()).Returns(product);

        var eventBus = Substitute.For<IEventBus>();
        var handler = new ProcessZeroAmountCheckoutCommandHandler(repository, eventBus);
        await handler.Handle(new ProcessZeroAmountCheckoutCommand(orgId, session.Id), CancellationToken.None);

        await eventBus.Received(1).PublishAsync(Arg.Is<ZeroAmountCheckoutCompletedIntegrationEvent>(e =>
            e.OriginalAmount == 100m && e.DiscountAmount == 100m));
    }

    [Test]
    public async Task SubscriptionPayment_Billplz_DoesNotClearReminderOnly()
    {
        using var db = CreateDb(out var orgId);
        var product = CreateProduct(orgId, gatewayName: "BILLPLZ");
        var sub = new Subscription(orgId, Guid.CreateVersion7(), product.Id);
        sub.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-5), isReminderOnly: true);
        sub.MarkAsPastDue();
        db.Products.Add(product);
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        var handler = CreateOpenCheckoutPaymentHandler(db, sub.ClientProfileId);
        await handler.HandleAsync(CreateCommercePaymentCompleted(orgId, sub.Id, "cus_junk", "tok_junk"));

        var reloaded = await db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == sub.Id);
        reloaded.Status.Should().Be("ACTIVE");
        reloaded.IsReminderOnly.Should().BeTrue();
        reloaded.VaultedTokenId.Should().BeNull();
    }

    [Test]
    public async Task SubscriptionPayment_Stripe_MayVaultAndClearReminderOnly()
    {
        using var db = CreateDb(out var orgId);
        var product = CreateProduct(orgId, gatewayName: "STRIPE");
        var sub = new Subscription(orgId, Guid.CreateVersion7(), product.Id);
        sub.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-5), isReminderOnly: true);
        sub.MarkAsPastDue();
        db.Products.Add(product);
        db.Subscriptions.Add(sub);
        await db.SaveChangesAsync();

        var handler = CreateOpenCheckoutPaymentHandler(db, sub.ClientProfileId);
        await handler.HandleAsync(CreateCommercePaymentCompleted(orgId, sub.Id, "cus_new", "pm_new"));

        var reloaded = await db.Subscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == sub.Id);
        reloaded.IsReminderOnly.Should().BeFalse();
        reloaded.VaultedCustomerId.Should().Be("cus_new");
        reloaded.VaultedTokenId.Should().Be("pm_new");
    }

    [Test]
    public async Task InitiateCheckout_FixedOneTime_Qty3_SendsUnitNetAndQuantity()
    {
        // Adapters still multiply Amount * Quantity (see GatewayCommonTests qty=2 → 2100).
        // Pre-multiplying here would square the charge (100 × 3 × 3 = 900).
        var orgId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var product = CreateProduct(orgId, interval: "one_time");

        CheckoutSession? session = null;
        GenerateCheckoutSessionQuery? payments = null;
        var repository = Substitute.For<ICommerceRepository>();
        repository.GetProductBySlugAsync(orgId, "pro-plan", Arg.Any<CancellationToken>()).Returns(product);
        repository.When(r => r.AddCheckoutSession(Arg.Any<CheckoutSession>()))
            .Do(ci => session = ci.Arg<CheckoutSession>());

        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ResolveClientProfileCommand>(), Arg.Any<CancellationToken>()).Returns(clientId);
        mediator.Send(Arg.Do<GenerateCheckoutSessionQuery>(q => payments = q), Arg.Any<CancellationToken>())
            .Returns("https://gateway.test/pay/xyz");

        var handler = CreateInitiateHandler(orgId, repository, mediator);
        var result = await handler.Handle(GuestCheckoutCommand(couponCode: null, quantity: 3), CancellationToken.None);

        session.Should().NotBeNull();
        session!.Quantity.Should().Be(3);
        session.Status.Should().Be("OPEN");
        result.IsZeroAmountBypass.Should().BeFalse();

        payments.Should().NotBeNull();
        payments!.Amount.Should().Be(100m);
        payments.Quantity.Should().Be(3);
        (payments.Amount * payments.Quantity).Should().Be(300m);
        payments.Amount.Should().NotBe(300m);
        payments.Amount.Should().NotBe(900m);
    }

    [Test]
    public async Task InitiateCheckout_FixedOneTime_Qty3_TenPercentCoupon_SendsUnitNetNinety()
    {
        var orgId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var product = CreateProduct(orgId, interval: "one_time");
        var coupon = new Coupon(orgId, "SAVE10", "PERCENTAGE", 10m, maxUses: 10, expiresAt: null);

        CheckoutSession? session = null;
        var repository = Substitute.For<ICommerceRepository>();
        repository.GetProductBySlugAsync(orgId, "pro-plan", Arg.Any<CancellationToken>()).Returns(product);
        repository.GetCouponByCodeWithLockAsync(orgId, "SAVE10", Arg.Any<CancellationToken>()).Returns(coupon);
        repository.When(r => r.AddCheckoutSession(Arg.Any<CheckoutSession>()))
            .Do(ci => session = ci.Arg<CheckoutSession>());

        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ResolveClientProfileCommand>(), Arg.Any<CancellationToken>()).Returns(clientId);
        mediator.Send(Arg.Any<GenerateCheckoutSessionQuery>(), Arg.Any<CancellationToken>())
            .Returns("https://gateway.test/pay/xyz");

        var handler = CreateInitiateHandler(orgId, repository, mediator);
        await handler.Handle(GuestCheckoutCommand("SAVE10", quantity: 3), CancellationToken.None);

        session.Should().NotBeNull();
        session!.Quantity.Should().Be(3);
        await mediator.Received(1).Send(
            Arg.Is<GenerateCheckoutSessionQuery>(q => q.Amount == 90m && q.Quantity == 3),
            Arg.Any<CancellationToken>());
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(100)]
    public async Task InitiateCheckout_FixedOneTime_OutOfRangeQuantity_ThrowsBeforePersist(int quantity)
    {
        var orgId = Guid.CreateVersion7();
        var product = CreateProduct(orgId, interval: "one_time");
        var repository = Substitute.For<ICommerceRepository>();
        repository.GetProductBySlugAsync(orgId, "pro-plan", Arg.Any<CancellationToken>()).Returns(product);
        var mediator = Substitute.For<IMediator>();
        var handler = CreateInitiateHandler(orgId, repository, mediator);

        var act = async () => await handler.Handle(
            GuestCheckoutCommand(couponCode: null, quantity: quantity),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*between 1 and 99*");
        repository.DidNotReceive().AddCheckoutSession(Arg.Any<CheckoutSession>());
        await mediator.DidNotReceive().Send(Arg.Any<ResolveClientProfileCommand>(), Arg.Any<CancellationToken>());
    }

    [TestCase("mo", "FIXED", 3)]
    [TestCase("yr", "FIXED", 2)]
    public async Task InitiateCheckout_FixedRecurring_NonOneQuantity_Persists(
        string interval,
        string pricingModel,
        int quantity)
    {
        var orgId = Guid.CreateVersion7();
        var product = CreateProduct(orgId, interval: interval, pricingModel: pricingModel);
        var repository = Substitute.For<ICommerceRepository>();
        repository.GetProductBySlugAsync(orgId, "pro-plan", Arg.Any<CancellationToken>()).Returns(product);
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ResolveClientProfileCommand>(), Arg.Any<CancellationToken>()).Returns(Guid.CreateVersion7());
        mediator.Send(Arg.Any<GenerateCheckoutSessionQuery>(), Arg.Any<CancellationToken>())
            .Returns("https://gateway.test/pay");
        var handler = CreateInitiateHandler(orgId, repository, mediator);

        await handler.Handle(GuestCheckoutCommand(couponCode: null, quantity: quantity), CancellationToken.None);

        repository.Received(1).AddCheckoutSession(Arg.Is<CheckoutSession>(s => s.Quantity == quantity));
    }

    [TestCase("one_time", "PWYW", 2)]
    [TestCase("one_time", "PWYW", 3)]
    public async Task InitiateCheckout_Pwyw_NonOneQuantity_ThrowsAndDoesNotPersist(
        string interval,
        string pricingModel,
        int quantity)
    {
        var orgId = Guid.CreateVersion7();
        var product = CreateProduct(orgId, interval: interval, pricingModel: pricingModel);
        var repository = Substitute.For<ICommerceRepository>();
        repository.GetProductBySlugAsync(orgId, "pro-plan", Arg.Any<CancellationToken>()).Returns(product);
        var mediator = Substitute.For<IMediator>();
        var handler = CreateInitiateHandler(orgId, repository, mediator);

        var act = async () => await handler.Handle(
            GuestCheckoutCommand(couponCode: null, quantity: quantity),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*fixed-price*");
        repository.DidNotReceive().AddCheckoutSession(Arg.Any<CheckoutSession>());
        await mediator.DidNotReceive().Send(Arg.Any<ResolveClientProfileCommand>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task InitiateCheckout_FixedOneTime_Qty3_PersistsSessionAndPaidOrderQuantity()
    {
        using var db = CreateDb(out var orgId);
        var product = CreateProduct(orgId, interval: "one_time");
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var clientId = Guid.CreateVersion7();
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ResolveClientProfileCommand>(), Arg.Any<CancellationToken>()).Returns(clientId);
        mediator.Send(Arg.Any<GenerateCheckoutSessionQuery>(), Arg.Any<CancellationToken>())
            .Returns("https://gateway.test/pay/xyz");

        var handler = CreateInitiateHandler(orgId, new CommerceRepository(db), mediator);
        await handler.Handle(GuestCheckoutCommand(couponCode: null, quantity: 3), CancellationToken.None);

        var session = await db.CheckoutSessions.IgnoreQueryFilters().SingleAsync();
        session.Quantity.Should().Be(3);
        session.Status.Should().Be("OPEN");
        session.ProductId.Should().Be(product.Id);

        var paymentHandler = CreateOpenCheckoutPaymentHandler(db, clientId);
        await paymentHandler.HandleAsync(CreateCommercePaymentCompleted(orgId, session.Id, amountPaid: 300m));

        var reloaded = await db.CheckoutSessions.IgnoreQueryFilters().SingleAsync(s => s.Id == session.Id);
        reloaded.Quantity.Should().Be(3);
        reloaded.Status.Should().Be("COMPLETED");

        (await db.Subscriptions.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        var order = await db.Orders.IgnoreQueryFilters().SingleAsync();
        order.Quantity.Should().Be(3);
        order.AmountPaid.Should().Be(300m);
        order.Status.Should().Be("COMPLETED");
    }

    [Test]
    public async Task InitiateCheckout_CustomSession_StillSendsLineSumAndQuantityOne()
    {
        var orgId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var session = new CheckoutSession(
            orgId,
            clientId,
            new[] { new AdHocLineItem("Consulting", 2, 250m) },
            DateTime.UtcNow.AddDays(1),
            isB2bRequired: false);

        var repository = Substitute.For<ICommerceRepository>();
        repository.GetCheckoutSessionByIdAsync(Arg.Any<Guid>(), session.Id, Arg.Any<CancellationToken>()).Returns(session);

        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GenerateCheckoutSessionQuery>(), Arg.Any<CancellationToken>())
            .Returns("https://gateway.test/pay/custom");

        var handler = CreateInitiateHandler(orgId, repository, mediator);
        var command = new InitiateCheckoutCommand(
            "acme",
            "pro-plan",
            "Ada",
            "ada@example.com",
            Phone: null,
            TaxId: null,
            IdType: null,
            IdValue: null,
            CompanyName: null,
            AddressLine1: null,
            City: null,
            PostalCode: null,
            StateCode: null,
            CountryCode: null,
            Quantity: 3,
            IsGuestCheckout: true,
            CouponCode: null,
            SessionId: session.Id);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Url.Should().Be("https://gateway.test/pay/custom");
        await mediator.Received(1).Send(
            Arg.Is<GenerateCheckoutSessionQuery>(q => q.Amount == 500m && q.Quantity == 1),
            Arg.Any<CancellationToken>());
        await repository.DidNotReceive().GetProductBySlugAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task InitiateCheckout_HundredPercentCoupon_Qty3_WritesZeroAmountOrderWithQuantity()
    {
        var orgId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var product = CreateProduct(orgId, interval: "one_time");
        var coupon = new Coupon(orgId, "FREE100", "PERCENTAGE", 100m, maxUses: 10, expiresAt: null);

        CheckoutSession? session = null;
        var orders = new List<Order>();
        var repository = Substitute.For<ICommerceRepository>();
        repository.GetProductBySlugAsync(orgId, "pro-plan", Arg.Any<CancellationToken>()).Returns(product);
        repository.GetCouponByCodeWithLockAsync(orgId, "FREE100", Arg.Any<CancellationToken>()).Returns(coupon);
        repository.When(r => r.AddCheckoutSession(Arg.Any<CheckoutSession>()))
            .Do(ci => session = ci.Arg<CheckoutSession>());
        // GetCheckoutSessionByIdAsync(organizationId, sessionId) — two Guid args.
        // Arg<Guid>() is ambiguous (same NSubstitute pitfall as issue 165).
        repository.GetCheckoutSessionByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => session != null && session.Id == ci.ArgAt<Guid>(1) ? session : null);
        repository.GetProductByIdAsync(Arg.Any<Guid>(), product.Id, Arg.Any<CancellationToken>()).Returns(product);
        repository.GetCouponByIdAsync(Arg.Any<Guid>(), coupon.Id, Arg.Any<CancellationToken>()).Returns(coupon);
        repository.When(r => r.AddOrder(Arg.Any<Order>())).Do(ci => orders.Add(ci.Arg<Order>()));

        var eventBus = Substitute.For<IEventBus>();
        var zeroHandler = new ProcessZeroAmountCheckoutCommandHandler(repository, eventBus);

        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ResolveClientProfileCommand>(), Arg.Any<CancellationToken>()).Returns(clientId);
        mediator.Send(Arg.Any<ProcessZeroAmountCheckoutCommand>(), Arg.Any<CancellationToken>())
            .Returns(ci => zeroHandler.Handle(
                ci.Arg<ProcessZeroAmountCheckoutCommand>(),
                ci.Arg<CancellationToken>()));

        var handler = CreateInitiateHandler(orgId, repository, mediator);
        var result = await handler.Handle(GuestCheckoutCommand("FREE100", quantity: 3), CancellationToken.None);

        session.Should().NotBeNull();
        session!.Status.Should().Be("COMPLETED");
        session.Quantity.Should().Be(3);
        result.IsZeroAmountBypass.Should().BeTrue();
        orders.Should().HaveCount(1);
        orders[0].AmountPaid.Should().Be(0m);
        orders[0].Quantity.Should().Be(3);

        await eventBus.Received(1).PublishAsync(Arg.Is<ZeroAmountCheckoutCompletedIntegrationEvent>(e =>
            e.OriginalAmount == 300m && e.DiscountAmount == 300m));
    }

    [Test]
    public async Task MarkCheckoutAsPaidOffline_OneTime_Qty3_WritesLineTotalOrder()
    {
        var orgId = Guid.CreateVersion7();
        var product = CreateProduct(orgId, interval: "one_time");
        var clientId = Guid.CreateVersion7();
        var session = new CheckoutSession(
            orgId, clientId, product.Id, couponId: null, DateTime.UtcNow.AddHours(1), quantity: 3);

        var orders = new List<Order>();
        var repository = Substitute.For<ICommerceRepository>();
        repository.GetCheckoutSessionByIdAsync(Arg.Any<Guid>(), session.Id, Arg.Any<CancellationToken>()).Returns(session);
        repository.GetProductByIdAsync(Arg.Any<Guid>(), product.Id, Arg.Any<CancellationToken>()).Returns(product);
        repository.When(r => r.AddOrder(Arg.Any<Order>())).Do(ci => orders.Add(ci.Arg<Order>()));

        var eventBus = Substitute.For<IEventBus>();
        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(Arg.Any<Guid>(), clientId).Returns(new ClientProfileDto
        {
            Id = clientId.ToString(),
            Full_name = "Offline Buyer",
            Email = "offline@example.com"
        });

        var handler = CreateMarkPaidHandler(repository, eventBus, crm);
        await handler.Handle(new MarkCheckoutAsPaidOfflineCommand(orgId, session.Id), CancellationToken.None);

        session.Status.Should().Be("COMPLETED");
        orders.Should().HaveCount(1);
        orders[0].AmountPaid.Should().Be(300m);
        orders[0].Quantity.Should().Be(3);
        await eventBus.Received().PublishAsync(Arg.Any<OrderCompletedIntegrationEvent>());
    }

    [Test]
    public async Task GatewayPaymentCompleted_OneTime_Qty3_WritesOrderQuantity()
    {
        using var db = CreateDb(out var orgId);
        var product = CreateProduct(orgId, interval: "one_time");
        var clientId = Guid.CreateVersion7();
        var session = new CheckoutSession(
            orgId, clientId, product.Id, couponId: null, DateTime.UtcNow.AddHours(1), quantity: 3);

        db.Products.Add(product);
        db.CheckoutSessions.Add(session);
        await db.SaveChangesAsync();

        var handler = CreateOpenCheckoutPaymentHandler(db, clientId);
        await handler.HandleAsync(CreateCommercePaymentCompleted(orgId, session.Id, amountPaid: 300m));

        (await db.Subscriptions.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        var order = await db.Orders.IgnoreQueryFilters().SingleAsync();
        order.Quantity.Should().Be(3);
        order.AmountPaid.Should().Be(300m);
        order.Status.Should().Be("COMPLETED");
    }

    private static InitiateCheckoutCommand GuestCheckoutCommand(string? couponCode, int quantity = 1) =>
        new(
            "acme",
            "pro-plan",
            "Ada",
            "ada@example.com",
            Phone: null,
            TaxId: null,
            IdType: null,
            IdValue: null,
            CompanyName: null,
            AddressLine1: null,
            City: null,
            PostalCode: null,
            StateCode: null,
            CountryCode: null,
            Quantity: quantity,
            IsGuestCheckout: true,
            CouponCode: couponCode);

    private static InitiateCheckoutCommandHandler CreateInitiateHandler(
        Guid orgId,
        ICommerceRepository repository,
        IMediator mediator)
    {
        var one = Substitute.For<IOneQueryService>();
        one.GetTenantIdBySlugAsync("acme").Returns(orgId);

        var comms = Substitute.For<ICommunicationsQueryService>();
        comms.HasValidEmailConfigAsync(orgId).Returns(true);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:ClientUrl"] = "https://portal.test"
            })
            .Build();

        // Issue 167: MerchantHasSstAsync fail-closes when IBillingQueryService is null.
        // Empty SST so quantity / coupon money asserts stay at net (do not reuse
        // QuoteOfflineSstTests registered SST).
        return new InitiateCheckoutCommandHandler(
            one, repository, mediator, config, comms, CommerceBillingStubs.NoSstBilling());
    }

    private static MarkCheckoutAsPaidOfflineCommandHandler CreateMarkPaidHandler(
        ICommerceRepository repository,
        IEventBus eventBus,
        ICrmQueryService crm) =>
        // Same 167 contract as CreateInitiateHandler — merchant not SST-registered.
        new(repository, eventBus, crm, CommerceBillingStubs.NoSstBilling());

    private static GatewayPaymentCompletedIntegrationEventHandler CreateOpenCheckoutPaymentHandler(
        CommerceDbContext db,
        Guid clientId)
    {
        var repository = Substitute.For<ICommerceRepository>();
        repository.When(r => r.AddSubscription(Arg.Any<Subscription>()))
            .Do(ci => db.Subscriptions.Add(ci.Arg<Subscription>()));
        repository.When(r => r.AddOrder(Arg.Any<Order>()))
            .Do(ci => db.Orders.Add(ci.Arg<Order>()));
        repository.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => db.SaveChangesAsync(callInfo.Arg<CancellationToken>()));

        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(Arg.Any<Guid>(), clientId).Returns(new ClientProfileDto
        {
            Id = clientId.ToString(),
            Full_name = "Test User",
            Email = "test@example.com"
        });

        return new GatewayPaymentCompletedIntegrationEventHandler(
            repository,
            Substitute.For<IEventBus>(),
            crm,
            db);
    }

    private static GatewayPaymentCompletedIntegrationEvent CreateCommercePaymentCompleted(
        Guid orgId,
        Guid sessionId,
        string? customerId = null,
        string? tokenId = null,
        decimal amountPaid = 100m) =>
        new(
            OrganizationId: orgId,
            GatewayTransactionId: "pi_test_replay",
            AmountPaid: amountPaid,
            Currency: "MYR",
            GatewayFee: 1m,
            TaxAmount: 0m,
            NetAmount: 99m,
            FxRate: 1m,
            BaseCurrency: "MYR",
            LineItems: new List<LineItemDto>(),
            Metadata: new Dictionary<string, string>
            {
                ["type"] = "commerce_subscription",
                ["subscription_id"] = sessionId.ToString(),
                ["tenant_id"] = orgId.ToString()
            },
            GatewayCustomerId: customerId,
            GatewayTokenId: tokenId);
}
