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
using Modules.Commerce.Infrastructure.Workers;
using Modules.CRM.Contracts;
using Modules.Payments.Contracts.Events;
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

    private static Product CreateProduct(Guid orgId, string interval = "mo", bool requiresPhone = false, bool requiresAddress = false, bool requiresTaxId = false)
    {
        return new Product(
            orgId,
            "Pro Plan",
            "pro-plan",
            100m,
            "FIXED",
            0m,
            "MYR",
            interval,
            "STRIPE",
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
        crm.GetClientProfileAsync(clientId).Returns(new ClientProfileDto
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
        repository.GetSubscriptionByIdAsync(sub.Id, Arg.Any<CancellationToken>()).Returns(sub);
        repository.GetProductByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);

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
        repository.GetCheckoutSessionByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        repository.GetProductByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);
        repository.When(r => r.AddSubscription(Arg.Any<Subscription>())).Do(ci => subscriptions.Add(ci.Arg<Subscription>()));
        repository.When(r => r.AddTransactionLog(Arg.Any<CommerceTransactionLog>())).Do(ci => logs.Add(ci.Arg<CommerceTransactionLog>()));

        var eventBus = Substitute.For<IEventBus>();
        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(clientId).Returns(new ClientProfileDto
        {
            Id = clientId.ToString(),
            Full_name = "Offline Buyer",
            Email = "offline@example.com"
        });

        var handler = new MarkCheckoutAsPaidOfflineCommandHandler(repository, eventBus, crm);
        await handler.Handle(new MarkCheckoutAsPaidOfflineCommand(orgId, session.Id), CancellationToken.None);

        session.Status.Should().Be("COMPLETED");
        subscriptions.Should().HaveCount(1);
        subscriptions[0].Status.Should().Be("ACTIVE");
        logs.Should().HaveCount(1);
        logs[0].Status.Should().Be("CONFIRMED");
        logs[0].RecordedByName.Should().Be("MANUAL_OFFLINE");

        await eventBus.Received().PublishAsync(Arg.Any<SubscriptionActivatedIntegrationEvent>());
        await eventBus.Received().PublishAsync(Arg.Any<ManualSubscriberEnrolledIntegrationEvent>());
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
        repository.GetCheckoutSessionByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        repository.When(r => r.AddSubscription(Arg.Any<Subscription>())).Do(ci => subscriptions.Add(ci.Arg<Subscription>()));
        repository.When(r => r.AddTransactionLog(Arg.Any<CommerceTransactionLog>())).Do(ci => logs.Add(ci.Arg<CommerceTransactionLog>()));

        var eventBus = Substitute.For<IEventBus>();
        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(clientId).Returns(new ClientProfileDto
        {
            Id = clientId.ToString(),
            Full_name = "Custom Buyer",
            Email = "custom@example.com"
        });

        var handler = new MarkCheckoutAsPaidOfflineCommandHandler(repository, eventBus, crm);
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

        var handler = new InitiateCheckoutCommandHandler(one, repository, mediator, config, comms);

        var act = async () => await handler.Handle(new InitiateCheckoutCommand(
            "acme",
            "pro-plan",
            "Ada",
            "ada@example.com",
            Phone: null,
            TaxId: null,
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
        var sub = new Subscription(orgId, clientId, product.Id);
        sub.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-10));
        sub.MarkAsPastDue();
        sub.AssignDunningCampaign(Guid.CreateVersion7());

        var logs = new List<CommerceTransactionLog>();
        var repository = Substitute.For<ICommerceRepository>();
        repository.GetSubscriptionByIdAsync(sub.Id, Arg.Any<CancellationToken>()).Returns(sub);
        repository.GetProductByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);
        repository.When(r => r.AddTransactionLog(Arg.Any<CommerceTransactionLog>())).Do(ci => logs.Add(ci.Arg<CommerceTransactionLog>()));

        var eventBus = Substitute.For<IEventBus>();
        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(clientId).Returns(new ClientProfileDto
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
        logs.Should().HaveCount(1);
        logs[0].RecordedByName.Should().Be("BANK_TRANSFER");
        await eventBus.Received().PublishAsync(Arg.Any<ManualSubscriberEnrolledIntegrationEvent>());
        await eventBus.Received().PublishAsync(Arg.Any<SubscriptionActivatedIntegrationEvent>());
    }
}
