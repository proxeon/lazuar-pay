using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Domain;
using FluentAssertions;
using Modules.Lhdn.Application.Commands;
using Modules.Lhdn.Application.Ports;
using Modules.Lhdn.Domain.Aggregates;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Lhdn;

[TestFixture]
public class CancelTaxDocumentCommandTests
{
    [Test]
    public async Task ValidWithinWindow_CallsGatewayAndCancels()
    {
        var org = Guid.CreateVersion7();
        var repo = Substitute.For<ILhdnRepository>();
        var gateway = Substitute.For<ILhdnGatewayAdapter>();
        var bus = Substitute.For<IEventBus>();
        var vault = Substitute.For<ISecretVault>();
        vault.DecryptOrPlaintext("secret").Returns("plain");

        var config = new LhdnTenantConfig(org, false, "C1", "BRN", "1");
        config.UpdateApiCredentials("cid", "secret");
        repo.GetTenantConfigAsync(org, Arg.Any<CancellationToken>()).Returns(config);

        var doc = new TaxDocument(org, "INV-2026-1", "h", "<Invoice/>");
        doc.MarkAsSubmitted("sub", "uuid-1");
        doc.MarkAsValid("long");
        repo.GetTaxDocumentByInternalIdAsync(org, "INV-2026-1", Arg.Any<CancellationToken>()).Returns(doc);

        gateway.GetTokenAsync(org, "cid", "plain", false, "C1", Arg.Any<CancellationToken>()).Returns("tok");
        gateway.CancelDocumentAsync("cid", "tok", "uuid-1", "wrong TIN", false, "C1", Arg.Any<CancellationToken>())
            .Returns(new LhdnCancelResult(true, "cancelled", null));

        var handler = new CancelTaxDocumentCommandHandler(repo, gateway, bus, vault);
        await handler.Handle(new CancelTaxDocumentCommand(org, "INV-2026-1", "wrong TIN"), CancellationToken.None);

        await gateway.Received(1).CancelDocumentAsync("cid", "tok", "uuid-1", "wrong TIN", false, "C1", Arg.Any<CancellationToken>());
        doc.ValidationStatus.Should().Be("CANCELLED");
    }

    [Test]
    public async Task After72Hours_DomainRefuses()
    {
        var org = Guid.CreateVersion7();
        var repo = Substitute.For<ILhdnRepository>();
        var doc = new TaxDocument(org, "INV-OLD", "h", "<Invoice/>");
        doc.MarkAsSubmitted("sub", "uuid-1");
        doc.MarkAsValid("long");
        typeof(TaxDocument).GetProperty(nameof(TaxDocument.ValidatedAt))!
            .SetValue(doc, DateTime.UtcNow.AddHours(-80));
        repo.GetTaxDocumentByInternalIdAsync(org, "INV-OLD", Arg.Any<CancellationToken>()).Returns(doc);
        repo.GetTenantConfigAsync(org, Arg.Any<CancellationToken>())
            .Returns(new LhdnTenantConfig(org, false, "C1", "BRN", "1"));

        var handler = new CancelTaxDocumentCommandHandler(
            repo, Substitute.For<ILhdnGatewayAdapter>(), Substitute.For<IEventBus>(), Substitute.For<ISecretVault>());

        var act = () => handler.Handle(new CancelTaxDocumentCommand(org, "INV-OLD", "late"), CancellationToken.None);
        await act.Should().ThrowAsync<Exception>();
    }

    [Test]
    public async Task UnknownInternalId_Throws()
    {
        var org = Guid.CreateVersion7();
        var repo = Substitute.For<ILhdnRepository>();
        var handler = new CancelTaxDocumentCommandHandler(
            repo, Substitute.For<ILhdnGatewayAdapter>(), Substitute.For<IEventBus>(), Substitute.For<ISecretVault>());
        var act = () => handler.Handle(new CancelTaxDocumentCommand(org, "ledger-guid", "x"), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }
}
