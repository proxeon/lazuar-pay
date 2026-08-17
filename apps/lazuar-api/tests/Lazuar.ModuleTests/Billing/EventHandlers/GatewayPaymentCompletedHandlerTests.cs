// Lazuar.ModuleTests/Billing/EventHandlers/GatewayPaymentCompletedHandlerTests.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Configuration;
using Modules.Billing.Application;
using Modules.Billing.Contracts;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Contracts.Events;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Infrastructure.EventHandlers;
using Modules.Payments.Contracts.Events;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Billing.EventHandlers;

[TestFixture]
public class GatewayPaymentCompletedHandlerTests
{
    private static Microsoft.Extensions.Configuration.IConfiguration Config() =>
        new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Lhdn:B2cIndividualThresholdMyr"] = "10000"
            })
            .Build();

    [Test]
    public async Task HandleAsync_ZeroAmount_DoesNotBookGmvOrAllocateReceipt()
    {
        var repository = Substitute.For<ILedgerRepository>();
        var mediator = Substitute.For<IMediator>();
        var eventBus = Substitute.For<IEventBus>();
        var handler = new GatewayPaymentCompletedHandler(repository, mediator, eventBus, Config());

        await handler.HandleAsync(new GatewayPaymentCompletedIntegrationEvent(
            OrganizationId: Guid.CreateVersion7(),
            GatewayTransactionId: "seti_zero",
            AmountPaid: 0m,
            Currency: "MYR",
            GatewayFee: 0m,
            TaxAmount: 0m,
            NetAmount: 0m,
            FxRate: 1m,
            BaseCurrency: "MYR",
            LineItems: new List<LineItemDto>(),
            Metadata: new Dictionary<string, string> { ["type"] = "trial" }));

        await repository.DidNotReceive().HasEntryBeenProcessedAsync(Arg.Any<string>(), Arg.Any<string>());
        repository.DidNotReceive().Add(Arg.Any<LedgerEntry>());
        await mediator.DidNotReceive().Send(Arg.Any<GenerateNextSequenceNumberCommand>(), Arg.Any<CancellationToken>());
        await eventBus.DidNotReceive().PublishAsync(Arg.Any<B2bTaxInvoiceRequestedIntegrationEvent>());
    }

    [Test]
    public async Task HandleAsync_WhenB2C_SavesChangesBeforeGeneratingDocument()
    {
        var repository = Substitute.For<ILedgerRepository>();
        var mediator = Substitute.For<IMediator>();
        var eventBus = Substitute.For<IEventBus>();
        var handler = new GatewayPaymentCompletedHandler(repository, mediator, eventBus, Config());

        var @event = new GatewayPaymentCompletedIntegrationEvent(
            OrganizationId: Guid.CreateVersion7(),
            GatewayTransactionId: "txn_123",
            AmountPaid: 100m,
            Currency: "MYR",
            GatewayFee: 2m,
            TaxAmount: 0m,
            NetAmount: 98m,
            FxRate: 1m,
            BaseCurrency: "MYR",
            LineItems: new List<LineItemDto>(),
            Metadata: new Dictionary<string, string> { { "is_b2b_required", "false" } }
        );

        repository.HasEntryBeenProcessedAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        mediator.Send(Arg.Any<GenerateNextSequenceNumberCommand>(), Arg.Any<CancellationToken>())
            .Returns("RCPT-2026");

        await handler.HandleAsync(@event);

        Received.InOrder(() =>
        {
            mediator.Send(Arg.Any<GenerateNextSequenceNumberCommand>(), Arg.Any<CancellationToken>());
            repository.SaveChangesAsync(Arg.Any<CancellationToken>());
            mediator.Send(Arg.Any<GenerateAndStoreDocumentCommand>(), Arg.Any<CancellationToken>());
        });
    }

    [Test]
    public async Task HandleAsync_WhenB2C_PassesSubscriptionIdAsDocumentCorrelation()
    {
        var repository = Substitute.For<ILedgerRepository>();
        var mediator = Substitute.For<IMediator>();
        var eventBus = Substitute.For<IEventBus>();
        var handler = new GatewayPaymentCompletedHandler(repository, mediator, eventBus, Config());
        var sessionId = Guid.CreateVersion7();

        var @event = new GatewayPaymentCompletedIntegrationEvent(
            OrganizationId: Guid.CreateVersion7(),
            GatewayTransactionId: "txn_123",
            AmountPaid: 100m,
            Currency: "MYR",
            GatewayFee: 2m,
            TaxAmount: 0m,
            NetAmount: 98m,
            FxRate: 1m,
            BaseCurrency: "MYR",
            LineItems: new List<LineItemDto>(),
            Metadata: new Dictionary<string, string>
            {
                { "is_b2b_required", "false" },
                { "subscription_id", sessionId.ToString() }
            }
        );

        repository.HasEntryBeenProcessedAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        mediator.Send(Arg.Any<GenerateNextSequenceNumberCommand>(), Arg.Any<CancellationToken>())
            .Returns("RCPT-2026");

        await handler.HandleAsync(@event);

        await mediator.Received().Send(
            Arg.Is<GenerateAndStoreDocumentCommand>(c => c.CorrelationId == sessionId.ToString()),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task HandleAsync_WhenB2B_BooksB2b_SkipsReceiptAndOfficialPdf()
    {
        var repository = Substitute.For<ILedgerRepository>();
        var mediator = Substitute.For<IMediator>();
        var eventBus = Substitute.For<IEventBus>();
        var handler = new GatewayPaymentCompletedHandler(repository, mediator, eventBus, Config());
        LedgerEntry? captured = null;
        repository.When(r => r.Add(Arg.Any<LedgerEntry>())).Do(ci => captured = ci.Arg<LedgerEntry>());

        var @event = new GatewayPaymentCompletedIntegrationEvent(
            OrganizationId: Guid.CreateVersion7(),
            GatewayTransactionId: "txn_b2b",
            AmountPaid: 100m,
            Currency: "MYR",
            GatewayFee: 2m,
            TaxAmount: 0m,
            NetAmount: 98m,
            FxRate: 1m,
            BaseCurrency: "MYR",
            LineItems: new List<LineItemDto>(),
            Metadata: new Dictionary<string, string> { { "is_b2b_required", "true" } }
        );

        repository.HasEntryBeenProcessedAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        mediator.Send(Arg.Any<GenerateNextSequenceNumberCommand>(), Arg.Any<CancellationToken>())
            .Returns("INV-2026-00001");

        await handler.HandleAsync(@event);

        captured.Should().NotBeNull();
        captured!.CustomerType.Should().Be("B2B");
        captured.ConsolidationStatus.Should().Be(ConsolidationStatuses.NotRequired);
        captured.CustomerDocumentNumber.Should().Be("INV-2026-00001");
        DocumentSeries.IsReceiptNumber(captured.CustomerDocumentNumber).Should().BeFalse();
        await mediator.Received().Send(
            Arg.Is<GenerateAndStoreDocumentCommand>(c => c.DocumentType == "Invoice"),
            Arg.Any<CancellationToken>());
        await mediator.DidNotReceive().Send(
            Arg.Is<GenerateAndStoreDocumentCommand>(c => c.DocumentType == "Tax Invoice"),
            Arg.Any<CancellationToken>());
        await mediator.DidNotReceive().Send(
            Arg.Is<GenerateAndStoreDocumentCommand>(c => c.DocumentType == "Official Receipt"),
            Arg.Any<CancellationToken>());
        await eventBus.Received(1).PublishAsync(Arg.Is<B2bTaxInvoiceRequestedIntegrationEvent>(e =>
            e.InvoiceNumber == "INV-2026-00001"
            && e.GatewayTransactionId == "txn_b2b"));
    }

    [Test]
    public async Task HandleAsync_RenewalWithSstMetadata_BooksTaxPayable()
    {
        var repository = Substitute.For<ILedgerRepository>();
        var mediator = Substitute.For<IMediator>();
        var eventBus = Substitute.For<IEventBus>();
        var handler = new GatewayPaymentCompletedHandler(repository, mediator, eventBus, Config());
        LedgerEntry? captured = null;
        repository.When(r => r.Add(Arg.Any<LedgerEntry>())).Do(ci => captured = ci.Arg<LedgerEntry>());

        var @event = new GatewayPaymentCompletedIntegrationEvent(
            OrganizationId: Guid.CreateVersion7(),
            GatewayTransactionId: "pi_renewal_sst",
            AmountPaid: 108m,
            Currency: "MYR",
            GatewayFee: 2m,
            TaxAmount: 0m,
            NetAmount: 106m,
            FxRate: 1m,
            BaseCurrency: "MYR",
            LineItems: new List<LineItemDto>(),
            Metadata: new Dictionary<string, string>
            {
                ["type"] = "commerce_subscription",
                ["sst_tax_amount"] = "8.00",
                ["sst_tax_type"] = "02"
            });

        repository.HasEntryBeenProcessedAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        mediator.Send(Arg.Any<GenerateNextSequenceNumberCommand>(), Arg.Any<CancellationToken>())
            .Returns("RCPT-2026");

        await handler.HandleAsync(@event);

        captured.Should().NotBeNull();
        captured!.Lines.Should().Contain(l => l.AccountType == AccountTypes.RevenueGross && l.Amount == -100m);
        captured.Lines.Should().Contain(l => l.AccountType == AccountTypes.LiabilityTaxPayable && l.Amount == -8m);
        captured.Lines.Sum(l => l.Amount).Should().Be(0m);
    }

    [Test]
    public async Task HandleAsync_B2bUsesResolvedSstNotRawEventTax()
    {
        var repository = Substitute.For<ILedgerRepository>();
        var mediator = Substitute.For<IMediator>();
        var eventBus = Substitute.For<IEventBus>();
        var handler = new GatewayPaymentCompletedHandler(repository, mediator, eventBus, Config());
        repository.HasEntryBeenProcessedAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        mediator.Send(Arg.Any<GenerateNextSequenceNumberCommand>(), Arg.Any<CancellationToken>())
            .Returns("INV-2026-00002");

        await handler.HandleAsync(new GatewayPaymentCompletedIntegrationEvent(
            OrganizationId: Guid.CreateVersion7(),
            GatewayTransactionId: "pi_b2b_sst",
            AmountPaid: 108m,
            Currency: "MYR",
            GatewayFee: 0m,
            TaxAmount: 0m,
            NetAmount: 108m,
            FxRate: 1m,
            BaseCurrency: "MYR",
            LineItems: new List<LineItemDto>(),
            Metadata: new Dictionary<string, string>
            {
                ["is_b2b_required"] = "true",
                ["sst_tax_amount"] = "8.00",
                ["sst_tax_type"] = "02"
            }));

        await eventBus.Received(1).PublishAsync(Arg.Is<B2bTaxInvoiceRequestedIntegrationEvent>(e =>
            e.TaxAmount == 8m && e.AmountExcludingTax == 100m));
    }
}
