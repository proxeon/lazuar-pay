using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Modules.Payments.Application;
using Modules.Payments.Application.Ports;
using Modules.Payments.Contracts.Events;
using Modules.Payments.Domain.Aggregates;
using Modules.Payments.Infrastructure.EventHandlers;
using Modules.Payments.Infrastructure.Gateways;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Payments;

[TestFixture]
public class ExecuteOffSessionChargeIntegrationEventHandlerTests
{
    private static ISecretVault CreateVault() =>
        new AesSecretVault(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kms:MasterKey"] = "test-master-key-for-unit-tests-32"
            })
            .Build());

    [Test]
    public async Task HandleAsync_PassesCorrelationArgsToChargeOffSession()
    {
        var tenantId = Guid.CreateVersion7();
        var subscriptionId = Guid.CreateVersion7();
        var campaignId = Guid.CreateVersion7();
        var chargeAttemptId = Guid.CreateVersion7();

        var config = new TenantPaymentConfiguration(tenantId, "STRIPE", "sk_test", "whsec", null);
        var configRepo = Substitute.For<ITenantPaymentConfigRepository>();
        configRepo.GetByTenantAndGatewayAsync(tenantId, "STRIPE")
            .Returns(config);

        Guid? capturedTenantId = null;
        string? capturedReceipt = null;
        Guid? capturedCampaignId = null;
        string? capturedIdempotencyKey = null;
        Guid? capturedChargeAttemptId = null;

        var adapter = Substitute.For<IPaymentGatewayAdapter>();
        adapter.ChargeOffSessionAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<decimal>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<Guid>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<Guid?>())
            .Returns(ci =>
            {
                capturedReceipt = ci.ArgAt<string>(6);
                capturedTenantId = ci.ArgAt<Guid>(7);
                capturedCampaignId = ci.ArgAt<Guid?>(8);
                capturedIdempotencyKey = ci.ArgAt<string?>(9);
                capturedChargeAttemptId = ci.ArgAt<Guid?>(10);
                return true;
            });

        var factory = Substitute.For<IPaymentGatewayFactory>();
        factory.GetAdapter("STRIPE").Returns(adapter);

        var eventBus = Substitute.For<IEventBus>();
        var handler = new ExecuteOffSessionChargeIntegrationEventHandler(
            configRepo,
            factory,
            eventBus,
            CreateVault(),
            Substitute.For<ILogger<ExecuteOffSessionChargeIntegrationEventHandler>>());

        var evt = new ExecuteOffSessionChargeIntegrationEvent(
            TenantId: tenantId,
            SubscriptionId: subscriptionId,
            Amount: 49.90m,
            Currency: "MYR",
            GatewayCustomerId: "cus_1",
            GatewayTokenId: "pm_1",
            DunningCampaignId: campaignId,
            GatewayName: "STRIPE",
            ChargeAttemptId: chargeAttemptId);
        await handler.HandleAsync(evt);

        // Adapter contract used by Stripe/CHIP/Razorpay to build off-session metadata:
        // type=commerce_subscription, subscription_id=receipt, tenant_id, dunning_campaign_id.
        capturedReceipt.Should().Be(subscriptionId.ToString());
        capturedTenantId.Should().Be(tenantId);
        capturedCampaignId.Should().Be(campaignId);
        capturedChargeAttemptId.Should().Be(chargeAttemptId);
        capturedIdempotencyKey.Should().Be(
            StripeGatewayAdapter.FormatOffSessionIdempotencyKey(chargeAttemptId));

        await eventBus.DidNotReceive().PublishAsync(Arg.Any<GatewayPaymentFailedIntegrationEvent>());
    }

    [Test]
    public async Task HandleAsync_ChargeDeclined_PublishesFailedEventWithCommerceMetadataKeys()
    {
        var tenantId = Guid.CreateVersion7();
        var subscriptionId = Guid.CreateVersion7();
        var campaignId = Guid.CreateVersion7();
        var chargeAttemptId = Guid.CreateVersion7();

        var config = new TenantPaymentConfiguration(tenantId, "STRIPE", "sk_test", "whsec", null);
        var configRepo = Substitute.For<ITenantPaymentConfigRepository>();
        configRepo.GetByTenantAndGatewayAsync(tenantId, "STRIPE").Returns(config);

        var adapter = Substitute.For<IPaymentGatewayAdapter>();
        adapter.ChargeOffSessionAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<string?>(),
                Arg.Any<Guid?>())
            .Returns(false);

        var factory = Substitute.For<IPaymentGatewayFactory>();
        factory.GetAdapter("STRIPE").Returns(adapter);

        var eventBus = Substitute.For<IEventBus>();
        var handler = new ExecuteOffSessionChargeIntegrationEventHandler(
            configRepo,
            factory,
            eventBus,
            CreateVault(),
            Substitute.For<ILogger<ExecuteOffSessionChargeIntegrationEventHandler>>());

        await handler.HandleAsync(new ExecuteOffSessionChargeIntegrationEvent(
            tenantId,
            subscriptionId,
            10m,
            "MYR",
            "cus",
            "pm",
            campaignId,
            "STRIPE",
            chargeAttemptId));

        await eventBus.Received(1).PublishAsync(Arg.Is<GatewayPaymentFailedIntegrationEvent>(e =>
            e.OrganizationId == tenantId
            && e.GatewayTransactionId == "off_session:" + subscriptionId
            && e.Metadata["type"] == "commerce_subscription"
            && e.Metadata["subscription_id"] == subscriptionId.ToString()
            && e.Metadata["tenant_id"] == tenantId.ToString()
            && e.Metadata["dunning_campaign_id"] == campaignId.ToString()
            && e.Metadata["charge_attempt_id"] == chargeAttemptId.ToString()
            && e.Metadata["failure_reason"] == "charge_declined"
            && e.Metadata["failure_source"] == "off_session"
            && e.Metadata["gateway_name"] == "STRIPE"));
    }

    [Test]
    public async Task HandleAsync_GatewayNotConfigured_PublishesFailedWithGatewayNotConfigured()
    {
        var tenantId = Guid.CreateVersion7();
        var subscriptionId = Guid.CreateVersion7();

        var configRepo = Substitute.For<ITenantPaymentConfigRepository>();
        configRepo.GetByTenantAndGatewayAsync(tenantId, "STRIPE")
            .Returns((TenantPaymentConfiguration?)null);

        var factory = Substitute.For<IPaymentGatewayFactory>();
        var eventBus = Substitute.For<IEventBus>();
        var handler = new ExecuteOffSessionChargeIntegrationEventHandler(
            configRepo,
            factory,
            eventBus,
            CreateVault(),
            Substitute.For<ILogger<ExecuteOffSessionChargeIntegrationEventHandler>>());

        await handler.HandleAsync(new ExecuteOffSessionChargeIntegrationEvent(
            tenantId,
            subscriptionId,
            10m,
            "MYR",
            "cus",
            "pm"));

        await eventBus.Received(1).PublishAsync(Arg.Is<GatewayPaymentFailedIntegrationEvent>(e =>
            e.Metadata["type"] == "commerce_subscription"
            && e.Metadata["subscription_id"] == subscriptionId.ToString()
            && e.Metadata["tenant_id"] == tenantId.ToString()
            && e.Metadata["failure_reason"] == "gateway_not_configured"
            && e.Metadata.ContainsKey("dunning_campaign_id") == false
            && e.Metadata.ContainsKey("charge_attempt_id") == false));

        factory.DidNotReceive().GetAdapter(Arg.Any<string>());
    }

    [Test]
    public async Task HandleAsync_Billplz_PublishesOffSessionNotSupported_DoesNotCallAdapter()
    {
        var tenantId = Guid.CreateVersion7();
        var subscriptionId = Guid.CreateVersion7();

        var configRepo = Substitute.For<ITenantPaymentConfigRepository>();
        var factory = Substitute.For<IPaymentGatewayFactory>();
        var eventBus = Substitute.For<IEventBus>();
        var handler = new ExecuteOffSessionChargeIntegrationEventHandler(
            configRepo,
            factory,
            eventBus,
            CreateVault(),
            Substitute.For<ILogger<ExecuteOffSessionChargeIntegrationEventHandler>>());

        await handler.HandleAsync(new ExecuteOffSessionChargeIntegrationEvent(
            tenantId,
            subscriptionId,
            10m,
            "MYR",
            "cus",
            "tok",
            GatewayName: "BILLPLZ"));

        await eventBus.Received(1).PublishAsync(Arg.Is<GatewayPaymentFailedIntegrationEvent>(e =>
            e.Metadata["failure_reason"] == "off_session_not_supported"
            && e.Metadata["failure_source"] == "off_session"
            && e.Metadata["gateway_name"] == "BILLPLZ"
            && e.Metadata["subscription_id"] == subscriptionId.ToString()));

        factory.DidNotReceive().GetAdapter(Arg.Any<string>());
        await configRepo.DidNotReceive().GetByTenantAndGatewayAsync(Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Test]
    public async Task HandleAsync_NotSupportedException_PublishesOffSessionNotSupported()
    {
        var tenantId = Guid.CreateVersion7();
        var subscriptionId = Guid.CreateVersion7();

        var config = new TenantPaymentConfiguration(tenantId, "STRIPE", "sk_test", "whsec", null);
        var configRepo = Substitute.For<ITenantPaymentConfigRepository>();
        configRepo.GetByTenantAndGatewayAsync(tenantId, "STRIPE").Returns(config);

        var adapter = Substitute.For<IPaymentGatewayAdapter>();
        adapter.ChargeOffSessionAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<string?>(),
                Arg.Any<Guid?>())
            .Returns<bool>(_ => throw new NotSupportedException("no vault"));

        var factory = Substitute.For<IPaymentGatewayFactory>();
        factory.GetAdapter("STRIPE").Returns(adapter);

        var eventBus = Substitute.For<IEventBus>();
        var handler = new ExecuteOffSessionChargeIntegrationEventHandler(
            configRepo,
            factory,
            eventBus,
            CreateVault(),
            Substitute.For<ILogger<ExecuteOffSessionChargeIntegrationEventHandler>>());

        await handler.HandleAsync(new ExecuteOffSessionChargeIntegrationEvent(
            tenantId,
            subscriptionId,
            10m,
            "MYR",
            "cus",
            "pm",
            GatewayName: "STRIPE"));

        await eventBus.Received(1).PublishAsync(Arg.Is<GatewayPaymentFailedIntegrationEvent>(e =>
            e.Metadata["failure_reason"] == "off_session_not_supported"));
    }

    [Test]
    public async Task HandleAsync_AdapterThrows_PublishesChargeException_DoesNotRethrow()
    {
        var tenantId = Guid.CreateVersion7();
        var subscriptionId = Guid.CreateVersion7();

        var config = new TenantPaymentConfiguration(tenantId, "STRIPE", "sk_test", "whsec", null);
        var configRepo = Substitute.For<ITenantPaymentConfigRepository>();
        configRepo.GetByTenantAndGatewayAsync(tenantId, "STRIPE").Returns(config);

        var adapter = Substitute.For<IPaymentGatewayAdapter>();
        adapter.ChargeOffSessionAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<string?>(),
                Arg.Any<Guid?>())
            .Returns<bool>(_ => throw new InvalidOperationException("transport timeout"));

        var factory = Substitute.For<IPaymentGatewayFactory>();
        factory.GetAdapter("STRIPE").Returns(adapter);

        var eventBus = Substitute.For<IEventBus>();
        var handler = new ExecuteOffSessionChargeIntegrationEventHandler(
            configRepo,
            factory,
            eventBus,
            CreateVault(),
            Substitute.For<ILogger<ExecuteOffSessionChargeIntegrationEventHandler>>());

        var act = () => handler.HandleAsync(new ExecuteOffSessionChargeIntegrationEvent(
            tenantId,
            subscriptionId,
            10m,
            "MYR",
            "cus",
            "pm",
            GatewayName: "STRIPE"));

        await act.Should().NotThrowAsync();
        await eventBus.Received(1).PublishAsync(Arg.Is<GatewayPaymentFailedIntegrationEvent>(e =>
            e.Metadata["failure_reason"] == "charge_exception"
            && e.Metadata["failure_source"] == "off_session"
            && e.Metadata["subscription_id"] == subscriptionId.ToString()));
    }

    [Test]
    public async Task HandleAsync_FactoryThrows_PublishesChargeException_DoesNotRethrow()
    {
        var tenantId = Guid.CreateVersion7();
        var subscriptionId = Guid.CreateVersion7();

        var config = new TenantPaymentConfiguration(tenantId, "STRIPE", "sk_test", "whsec", null);
        var configRepo = Substitute.For<ITenantPaymentConfigRepository>();
        configRepo.GetByTenantAndGatewayAsync(tenantId, "STRIPE").Returns(config);

        var factory = Substitute.For<IPaymentGatewayFactory>();
        factory.GetAdapter("STRIPE").Returns(_ => throw new InvalidOperationException("unknown gateway"));

        var eventBus = Substitute.For<IEventBus>();
        var handler = new ExecuteOffSessionChargeIntegrationEventHandler(
            configRepo,
            factory,
            eventBus,
            CreateVault(),
            Substitute.For<ILogger<ExecuteOffSessionChargeIntegrationEventHandler>>());

        var act = () => handler.HandleAsync(new ExecuteOffSessionChargeIntegrationEvent(
            tenantId,
            subscriptionId,
            10m,
            "MYR",
            "cus",
            "pm",
            GatewayName: "STRIPE"));

        await act.Should().NotThrowAsync();
        await eventBus.Received(1).PublishAsync(Arg.Is<GatewayPaymentFailedIntegrationEvent>(e =>
            e.Metadata["failure_reason"] == "charge_exception"
            && e.Metadata["subscription_id"] == subscriptionId.ToString()));
    }

    [Test]
    public async Task HandleAsync_OffSessionDeclined_PassesStripeDeclineCode()
    {
        var tenantId = Guid.CreateVersion7();
        var subscriptionId = Guid.CreateVersion7();
        var config = new TenantPaymentConfiguration(tenantId, "STRIPE", "sk_test", "whsec", null);
        var configRepo = Substitute.For<ITenantPaymentConfigRepository>();
        configRepo.GetByTenantAndGatewayAsync(tenantId, "STRIPE").Returns(config);

        var adapter = Substitute.For<IPaymentGatewayAdapter>();
        adapter.ChargeOffSessionAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<string?>(),
                Arg.Any<Guid?>())
            .Returns<bool>(_ => throw new OffSessionDeclinedException("stolen_card"));

        var factory = Substitute.For<IPaymentGatewayFactory>();
        factory.GetAdapter("STRIPE").Returns(adapter);
        var eventBus = Substitute.For<IEventBus>();
        var handler = new ExecuteOffSessionChargeIntegrationEventHandler(
            configRepo, factory, eventBus, CreateVault(),
            Substitute.For<ILogger<ExecuteOffSessionChargeIntegrationEventHandler>>());

        await handler.HandleAsync(new ExecuteOffSessionChargeIntegrationEvent(
            tenantId, subscriptionId, 10m, "MYR", "cus", "pm", GatewayName: "STRIPE"));

        await eventBus.Received(1).PublishAsync(Arg.Is<GatewayPaymentFailedIntegrationEvent>(e =>
            e.Metadata["failure_reason"] == "stolen_card"
            && e.Metadata["decline_code"] == "stolen_card"
            && e.Metadata["subscription_id"] == subscriptionId.ToString()));
    }
}
