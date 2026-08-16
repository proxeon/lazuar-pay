using System;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using FluentAssertions;
using Modules.Payments.Application.Ports;
using Modules.Payments.Contracts.Events;
using Modules.Payments.Domain.Aggregates;
using Modules.Payments.Infrastructure.EventHandlers;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Payments;

[TestFixture]
public class GatewayRefundRequestedIntegrationEventHandlerTests
{
    [Test]
    public async Task MissingConfig_PublishesFailed()
    {
        var orgId = Guid.CreateVersion7();
        var paymentId = Guid.CreateVersion7();
        var (handler, _, bus, _) = CreateHandler(orgId, config: null);

        await handler.HandleAsync(Requested(orgId, paymentId, 10m));

        await bus.Received(1).PublishAsync(Arg.Is<GatewayRefundFailedIntegrationEvent>(e =>
            e.PaymentRecordId == paymentId && e.ErrorMessage.Contains("configuration", StringComparison.OrdinalIgnoreCase)));
        await bus.DidNotReceive().PublishAsync(Arg.Any<GatewayRefundCompletedIntegrationEvent>());
    }

    [Test]
    public async Task AmountNotPositive_PublishesFailed()
    {
        var orgId = Guid.CreateVersion7();
        var paymentId = Guid.CreateVersion7();
        var config = new TenantPaymentConfiguration(orgId, "STRIPE", "sk_test", "whsec", null);
        var (handler, _, bus, _) = CreateHandler(orgId, config);

        await handler.HandleAsync(Requested(orgId, paymentId, 0m));

        await bus.Received(1).PublishAsync(Arg.Is<GatewayRefundFailedIntegrationEvent>(e =>
            e.ErrorMessage.Contains("greater than zero", StringComparison.OrdinalIgnoreCase)));
    }

    [Test]
    public async Task AdapterTrue_PublishesCompleted()
    {
        var orgId = Guid.CreateVersion7();
        var paymentId = Guid.CreateVersion7();
        var config = new TenantPaymentConfiguration(orgId, "STRIPE", "sk_test", "whsec", null);
        var (handler, adapter, bus, _) = CreateHandler(orgId, config);
        adapter.IssueRefundAsync("sk_test", "pi_1", 40m).Returns(true);

        await handler.HandleAsync(Requested(orgId, paymentId, 40m, isFull: false));

        await adapter.Received(1).IssueRefundAsync("sk_test", "pi_1", 40m);
        await bus.Received(1).PublishAsync(Arg.Is<GatewayRefundCompletedIntegrationEvent>(e =>
            e.RefundedAmount == 40m
            && e.RefundedFee == 0m
            && e.IsFullRefund == false
            && e.PaymentRecordId == paymentId));
    }

    [Test]
    public async Task AdapterFalse_PublishesFailed()
    {
        var orgId = Guid.CreateVersion7();
        var paymentId = Guid.CreateVersion7();
        var config = new TenantPaymentConfiguration(orgId, "STRIPE", "sk_test", "whsec", null);
        var (handler, adapter, bus, _) = CreateHandler(orgId, config);
        adapter.IssueRefundAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<decimal>()).Returns(false);

        await handler.HandleAsync(Requested(orgId, paymentId, 10m));

        await bus.Received(1).PublishAsync(Arg.Is<GatewayRefundFailedIntegrationEvent>(e =>
            e.ErrorMessage.Contains("adapter", StringComparison.OrdinalIgnoreCase)));
    }

    [Test]
    public async Task SoftDisabledConfig_StillCallsAdapter()
    {
        var orgId = Guid.CreateVersion7();
        var paymentId = Guid.CreateVersion7();
        var config = new TenantPaymentConfiguration(orgId, "STRIPE", "sk_test", "whsec", null, isActive: false);
        var (handler, adapter, _, _) = CreateHandler(orgId, config);
        adapter.IssueRefundAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<decimal>()).Returns(true);

        await handler.HandleAsync(Requested(orgId, paymentId, 10m));

        await adapter.Received(1).IssueRefundAsync("sk_test", "pi_1", 10m);
    }

    private static GatewayRefundRequestedIntegrationEvent Requested(
        Guid orgId, Guid paymentId, decimal amount, bool isFull = true) =>
        new(orgId, Guid.Empty, paymentId, "pi_1", amount, "MYR", "STRIPE", 0m, isFull);

    private static (
        GatewayRefundRequestedIntegrationEventHandler Handler,
        IPaymentGatewayAdapter Adapter,
        IEventBus Bus,
        ITenantPaymentConfigRepository Configs)
        CreateHandler(Guid orgId, TenantPaymentConfiguration? config)
    {
        var configs = Substitute.For<ITenantPaymentConfigRepository>();
        configs.GetByTenantAndGatewayAsync(orgId, "STRIPE").Returns(config);

        var adapter = Substitute.For<IPaymentGatewayAdapter>();
        var factory = Substitute.For<IPaymentGatewayFactory>();
        factory.GetAdapter("STRIPE").Returns(adapter);

        var bus = Substitute.For<IEventBus>();
        var vault = Substitute.For<ISecretVault>();
        vault.Decrypt(Arg.Any<string>()).Returns(ci => ci.ArgAt<string>(0));

        var handler = new GatewayRefundRequestedIntegrationEventHandler(configs, factory, bus, vault);
        return (handler, adapter, bus, configs);
    }
}
