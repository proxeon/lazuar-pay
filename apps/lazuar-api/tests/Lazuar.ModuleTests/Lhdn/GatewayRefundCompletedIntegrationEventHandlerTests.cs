using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Application.Services;
using Modules.Lhdn.Domain.Aggregates;
using Modules.Payments.Contracts.Events;
using NSubstitute;
using NUnit.Framework;
using LhdnRefundHandler = Modules.Lhdn.Infrastructure.EventHandlers.GatewayRefundCompletedIntegrationEventHandler;

namespace Lazuar.ModuleTests.Lhdn;

[TestFixture]
public class GatewayRefundCompletedIntegrationEventHandlerTests
{
    [Test]
    public async Task PartialRefund_DoesNotCancelDocument()
    {
        var orgId = Guid.CreateVersion7();
        var paymentId = Guid.CreateVersion7();
        var (handler, repo, gateway) = CreateHandler();

        await handler.HandleAsync(new GatewayRefundCompletedIntegrationEvent(
            orgId, Guid.Empty, paymentId, "pi_1", 40m, "MYR", 0m, 40m, 0m, IsFullRefund: false));

        await repo.DidNotReceive().GetTaxDocumentByInternalIdAsync(Arg.Any<Guid>(), Arg.Any<string>());
        await gateway.DidNotReceive().CancelDocumentAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<bool>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task FullRefund_Within72h_CancelsDocument()
    {
        var orgId = Guid.CreateVersion7();
        var paymentId = Guid.CreateVersion7();
        var doc = new TaxDocument(orgId, paymentId.ToString(), "hash", "<xml/>");
        doc.MarkAsSubmitted("sub-1", "lhdn-uuid-1");
        doc.MarkAsValid("long-1");

        var config = new LhdnTenantConfig(orgId, false, "C12345678901", "BRN", "202001012345");
        config.UpdateApiCredentials("client-id", "client-secret");

        var (handler, repo, gateway) = CreateHandler();
        repo.GetTaxDocumentByInternalIdAsync(orgId, paymentId.ToString()).Returns(doc);
        repo.GetTenantConfigAsync(orgId).Returns(config);
        gateway.GetTokenAsync(orgId, "client-id", "client-secret", false, config.SupplierTin)
            .Returns("token");
        gateway.CancelDocumentAsync("client-id", "token", "lhdn-uuid-1", Arg.Any<string>(), false, config.SupplierTin)
            .Returns(new LhdnCancelResult(true, "cancelled", null));

        await handler.HandleAsync(new GatewayRefundCompletedIntegrationEvent(
            orgId, Guid.Empty, paymentId, "pi_1", 100m, "MYR", 0m, 100m, 0m, IsFullRefund: true));

        await gateway.Received(1).CancelDocumentAsync(
            "client-id", "token", "lhdn-uuid-1", Arg.Any<string>(), false, config.SupplierTin);
        doc.ValidationStatus.Should().Be("CANCELLED");
        await repo.Received(1).SaveChangesAsync();
    }

    private static (LhdnRefundHandler Handler, ILhdnRepository Repo, ILhdnGatewayAdapter Gateway) CreateHandler()
    {
        var repo = Substitute.For<ILhdnRepository>();
        var gateway = Substitute.For<ILhdnGatewayAdapter>();
        var strategies = Substitute.For<IDocumentStrategyFactory>();
        var vault = Substitute.For<ISecretVault>();
        vault.Decrypt(Arg.Any<string>()).Returns(ci => ci.ArgAt<string>(0));

        var handler = new LhdnRefundHandler(
            repo,
            gateway,
            strategies,
            vault,
            NullLogger<LhdnRefundHandler>.Instance);
        return (handler, repo, gateway);
    }
}
