using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Payments.Application.Commands;
using Modules.Payments.Application.Ports;
using Modules.Payments.Contracts.Events;
using Modules.Payments.Domain.Aggregates;
using Modules.Payments.Domain.Entities;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Payments;

[TestFixture]
public class ProcessGatewayWebhookCommandHandlerTests
{
    [Test]
    public async Task Handle_PaymentFailed_Publishes_GatewayPaymentFailedIntegrationEvent()
    {
        var tenantId = Guid.CreateVersion7();
        var metadata = new Dictionary<string, string>
        {
            ["type"] = "commerce_subscription",
            ["subscription_id"] = Guid.CreateVersion7().ToString()
        };

        var config = new TenantPaymentConfiguration(tenantId, "STRIPE", "sk_test", "whsec_test", null);
        var configRepo = Substitute.For<ITenantPaymentConfigRepository>();
        configRepo.GetByTenantAndGatewayAsync(tenantId, "STRIPE", Arg.Any<CancellationToken>())
            .Returns(config);

        var logRepo = Substitute.For<IPaymentWebhookLogRepository>();
        logRepo.HasBeenProcessedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var adapter = Substitute.For<IPaymentGatewayAdapter>();
        adapter.GatewayType.Returns("STRIPE");
        adapter.ParseWebhookAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Dictionary<string, string>>(),
                Arg.Any<decimal>(), Arg.Any<decimal>(), Arg.Any<decimal>())
            .Returns(new GatewayWebhookParsedResult(
                Verified: true,
                EventType: "PAYMENT_FAILED",
                EventId: "evt_failed_1",
                AmountPaid: 0,
                Currency: "MYR",
                GatewayTransactionId: "pi_failed_1",
                Metadata: metadata,
                GatewayFee: 0,
                TaxAmount: 0,
                NetAmount: 0,
                FxRate: 1,
                BaseCurrency: "MYR",
                Error: null));

        var gatewayFactory = Substitute.For<IPaymentGatewayFactory>();
        gatewayFactory.GetAdapter("STRIPE").Returns(adapter);

        var eventBus = Substitute.For<IEventBus>();

        var handler = new ProcessGatewayWebhookCommandHandler(configRepo, logRepo, gatewayFactory, eventBus);

        await handler.Handle(
            new ProcessGatewayWebhookCommand(tenantId, "STRIPE", "{}", new Dictionary<string, string>()),
            CancellationToken.None);

        await eventBus.Received(1).PublishAsync(Arg.Is<GatewayPaymentFailedIntegrationEvent>(e =>
            e.OrganizationId == tenantId
            && e.GatewayTransactionId == "pi_failed_1"
            && e.Metadata["type"] == "commerce_subscription"));

        logRepo.Received(1).Add(Arg.Any<PaymentWebhookLog>());
        await logRepo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_PaymentFailed_Uses_EventId_When_GatewayTransactionId_Missing()
    {
        var tenantId = Guid.CreateVersion7();
        var config = new TenantPaymentConfiguration(tenantId, "STRIPE", "sk_test", "whsec_test", null);
        var configRepo = Substitute.For<ITenantPaymentConfigRepository>();
        configRepo.GetByTenantAndGatewayAsync(tenantId, "STRIPE", Arg.Any<CancellationToken>())
            .Returns(config);

        var logRepo = Substitute.For<IPaymentWebhookLogRepository>();
        logRepo.HasBeenProcessedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var adapter = Substitute.For<IPaymentGatewayAdapter>();
        adapter.GatewayType.Returns("STRIPE");
        adapter.ParseWebhookAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Dictionary<string, string>>(),
                Arg.Any<decimal>(), Arg.Any<decimal>(), Arg.Any<decimal>())
            .Returns(new GatewayWebhookParsedResult(
                Verified: true,
                EventType: "PAYMENT_FAILED",
                EventId: "evt_no_tx",
                AmountPaid: 0,
                Currency: "MYR",
                GatewayTransactionId: null,
                Metadata: new Dictionary<string, string>(),
                GatewayFee: 0,
                TaxAmount: 0,
                NetAmount: 0,
                FxRate: 1,
                BaseCurrency: "MYR",
                Error: null));

        var gatewayFactory = Substitute.For<IPaymentGatewayFactory>();
        gatewayFactory.GetAdapter("STRIPE").Returns(adapter);

        var eventBus = Substitute.For<IEventBus>();
        var handler = new ProcessGatewayWebhookCommandHandler(configRepo, logRepo, gatewayFactory, eventBus);

        await handler.Handle(
            new ProcessGatewayWebhookCommand(tenantId, "STRIPE", "{}", new Dictionary<string, string>()),
            CancellationToken.None);

        await eventBus.Received(1).PublishAsync(Arg.Is<GatewayPaymentFailedIntegrationEvent>(e =>
            e.GatewayTransactionId == "evt_no_tx"));
    }
}
