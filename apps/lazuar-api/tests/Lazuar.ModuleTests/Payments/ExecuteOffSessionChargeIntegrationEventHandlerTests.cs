using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Modules.Payments.Application.Ports;
using Modules.Payments.Contracts.Events;
using Modules.Payments.Domain.Aggregates;
using Modules.Payments.Infrastructure.EventHandlers;
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
                Arg.Any<Guid?>())
            .Returns(ci =>
            {
                capturedReceipt = ci.ArgAt<string>(6);
                capturedTenantId = ci.ArgAt<Guid>(7);
                capturedCampaignId = ci.ArgAt<Guid?>(8);
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

        await handler.HandleAsync(new ExecuteOffSessionChargeIntegrationEvent(
            TenantId: tenantId,
            SubscriptionId: subscriptionId,
            Amount: 49.90m,
            Currency: "MYR",
            GatewayCustomerId: "cus_1",
            GatewayTokenId: "pm_1",
            DunningCampaignId: campaignId,
            GatewayName: "STRIPE",
            ChargeAttemptId: chargeAttemptId));

        // Adapter contract used by Stripe/CHIP/Razorpay to build off-session metadata:
        // type=commerce_subscription, subscription_id=receipt, tenant_id, dunning_campaign_id.
        capturedReceipt.Should().Be(subscriptionId.ToString());
        capturedTenantId.Should().Be(tenantId);
        capturedCampaignId.Should().Be(campaignId);

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
                Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<Guid?>())
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
}
