using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using FluentAssertions;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Billing.Contracts;
using Modules.Billing.Contracts.Commands;
using Modules.Commerce.Contracts;
using Modules.CRM.Contracts;
using Modules.Lhdn.Application.Commands;
using Modules.Lhdn.Application.Ports;
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
    public async Task PartialRefund_Within72h_SubmitsCreditNote_DoesNotCancel()
    {
        var orgId = Guid.CreateVersion7();
        var paymentId = Guid.CreateVersion7();
        var eventId = Guid.CreateVersion7();
        var doc = new TaxDocument(orgId, "INV-2026-00001", "hash", "<xml/>");
        doc.MarkAsSubmitted("sub-1", "lhdn-uuid-1");
        doc.MarkAsValid("long-1");

        var (handler, repo, mediator, billing, crm, lookup) = CreateHandlerFull();
        billing.FindPaymentByGatewayTransactionAsync(orgId, "pi_1")
            .Returns(new LedgerDocumentIdentity(
                Guid.CreateVersion7(), "GATEWAY_PAYMENT", "pi_1", "INV-2026-00001",
                "lhdn-uuid-1", "INV-2026-00001", "B2B", "VALID", 100m, "MYR", DateTime.UtcNow));
        billing.FindLedgerByReferenceAsync(orgId, "GATEWAY_REFUND", paymentId.ToString("N") + ":" + eventId.ToString("N"))
            .Returns(new LedgerDocumentIdentity(
                Guid.CreateVersion7(), "GATEWAY_REFUND", "ref", "CN-2026-00040",
                null, "CN-2026-00040", "B2B", null, 40m, "MYR", DateTime.UtcNow));
        repo.GetTaxDocumentByInternalIdAsync(orgId, "INV-2026-00001").Returns(doc);
        lookup.GetCustomerForDocumentAsync(orgId, "pi_1", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new CommerceCustomerDisplay("Buyer Co", "buyer@example.com", "C98765432109"));
        crm.GetClientProfileByEmailAsync(orgId, "buyer@example.com")
            .Returns(new ClientProfileDto
            {
                Id = Guid.CreateVersion7().ToString(),
                Full_name = "Buyer Co",
                Email = "buyer@example.com",
                Phone = "60123456789",
                Tin = "C98765432109",
                Id_type = "BRN",
                Id_value = "202001099999",
                Consented_to_marketing = false
            });

        await handler.HandleAsync(new GatewayRefundCompletedIntegrationEvent(
            orgId, Guid.Empty, paymentId, "pi_1", 40m, "MYR", 0m, 40m, 0m, IsFullRefund: false)
        {
            Id = eventId
        });

        await mediator.DidNotReceive().Send(Arg.Any<CancelTaxDocumentCommand>(), Arg.Any<CancellationToken>());
        await mediator.Received(1).Send(
            Arg.Is<SubmitTaxDocumentCommand>(c =>
                c.Payload.Document_type == SubmitDocumentRequestDtoDocument_type._02
                && c.Payload.Internal_id == "CN-2026-00040"
                && c.Payload.Total_including_tax == 40),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task NoTaxDocument_IsNoOp()
    {
        var orgId = Guid.CreateVersion7();
        var (handler, _, mediator, _) = CreateHandler();

        await handler.HandleAsync(new GatewayRefundCompletedIntegrationEvent(
            orgId, Guid.Empty, Guid.CreateVersion7(), "pi_missing", 100m, "MYR", 0m, 100m, 0m, IsFullRefund: true));

        await mediator.DidNotReceive().Send(Arg.Any<CancelTaxDocumentCommand>(), Arg.Any<CancellationToken>());
        await mediator.DidNotReceive().Send(Arg.Any<SubmitTaxDocumentCommand>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task FullRefund_Within72h_SendsCancelCommand()
    {
        var orgId = Guid.CreateVersion7();
        var paymentId = Guid.CreateVersion7();
        var doc = new TaxDocument(orgId, "INV-2026-00001", "hash", "<xml/>");
        doc.MarkAsSubmitted("sub-1", "lhdn-uuid-1");
        doc.MarkAsValid("long-1");

        var (handler, repo, mediator, billing) = CreateHandler();
        billing.FindPaymentByGatewayTransactionAsync(orgId, "pi_1")
            .Returns(new LedgerDocumentIdentity(
                Guid.CreateVersion7(), "GATEWAY_PAYMENT", "pi_1", "INV-2026-00001",
                "lhdn-uuid-1", "INV-2026-00001", "B2B", "VALID", 100m, "MYR", DateTime.UtcNow));
        repo.GetTaxDocumentByInternalIdAsync(orgId, "INV-2026-00001").Returns(doc);

        await handler.HandleAsync(new GatewayRefundCompletedIntegrationEvent(
            orgId, Guid.Empty, paymentId, "pi_1", 100m, "MYR", 0m, 100m, 0m, IsFullRefund: true));

        await mediator.Received(1).Send(
            Arg.Is<CancelTaxDocumentCommand>(c => c.InternalId == "INV-2026-00001"),
            Arg.Any<CancellationToken>());
        await mediator.DidNotReceive().Send(Arg.Any<SubmitTaxDocumentCommand>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task FullRefund_After72h_SubmitsCreditNoteWithCrmTin()
    {
        var orgId = Guid.CreateVersion7();
        var paymentId = Guid.CreateVersion7();
        var eventId = Guid.CreateVersion7();
        var doc = new TaxDocument(orgId, "INV-2026-00002", "hash", "<xml/>");
        doc.MarkAsSubmitted("sub-2", "lhdn-uuid-2");
        doc.MarkAsValid("long-2");
        typeof(TaxDocument).GetProperty(nameof(TaxDocument.ValidatedAt))!
            .SetValue(doc, DateTime.UtcNow.AddHours(-80));

        var (handler, repo, mediator, billing, crm, lookup) = CreateHandlerFull();
        billing.FindPaymentByGatewayTransactionAsync(orgId, "pi_2")
            .Returns(new LedgerDocumentIdentity(
                Guid.CreateVersion7(), "GATEWAY_PAYMENT", "pi_2", "INV-2026-00002",
                "lhdn-uuid-2", "INV-2026-00002", "B2B", "VALID", 100m, "MYR", DateTime.UtcNow));
        billing.FindLedgerByReferenceAsync(orgId, "GATEWAY_REFUND", paymentId.ToString("N") + ":" + eventId.ToString("N"))
            .Returns(new LedgerDocumentIdentity(
                Guid.CreateVersion7(), "GATEWAY_REFUND", "ref", "CN-2026-00009",
                null, "CN-2026-00009", "B2C", null, 100m, "MYR", DateTime.UtcNow));
        repo.GetTaxDocumentByInternalIdAsync(orgId, "INV-2026-00002").Returns(doc);
        lookup.GetCustomerForDocumentAsync(orgId, "pi_2", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new CommerceCustomerDisplay("Buyer Co", "buyer@example.com", "C98765432109"));
        crm.GetClientProfileByEmailAsync(orgId, "buyer@example.com")
            .Returns(new ClientProfileDto
            {
                Id = Guid.CreateVersion7().ToString(),
                Full_name = "Buyer Co",
                Email = "buyer@example.com",
                Phone = "60123456789",
                Tin = "C98765432109",
                Id_type = "BRN",
                Id_value = "202001099999",
                Consented_to_marketing = false
            });

        var @event = new GatewayRefundCompletedIntegrationEvent(
            orgId, Guid.Empty, paymentId, "pi_2", 100m, "MYR", 0m, 100m, 0m, IsFullRefund: true)
        {
            Id = eventId
        };

        await handler.HandleAsync(@event);

        await mediator.DidNotReceive().Send(Arg.Any<CancelTaxDocumentCommand>(), Arg.Any<CancellationToken>());
        await mediator.Received(1).Send(
            Arg.Is<SubmitTaxDocumentCommand>(c =>
                c.Payload.Document_type == SubmitDocumentRequestDtoDocument_type._02
                && c.Payload.Internal_id == "CN-2026-00009"
                && c.Payload.Buyer_tin == "C98765432109"
                && c.Payload.Buyer_tin != "IG1234567890"
                && c.Payload.Total_including_tax == 100
                && c.Payload.Total_excluding_tax == 100
                && c.Payload.Total_tax == 0
                && !string.IsNullOrWhiteSpace(c.IdempotencyKey)),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task FullRefund_After72h_WithSst_DoesNotAddTaxTwice()
    {
        var orgId = Guid.CreateVersion7();
        var paymentId = Guid.CreateVersion7();
        var eventId = Guid.CreateVersion7();
        var doc = new TaxDocument(orgId, "INV-2026-00003", "hash", "<xml/>");
        doc.MarkAsSubmitted("sub-3", "lhdn-uuid-3");
        doc.MarkAsValid("long-3");
        typeof(TaxDocument).GetProperty(nameof(TaxDocument.ValidatedAt))!
            .SetValue(doc, DateTime.UtcNow.AddHours(-80));

        var (handler, repo, mediator, billing, crm, lookup) = CreateHandlerFull();
        billing.FindPaymentByGatewayTransactionAsync(orgId, "pi_3")
            .Returns(new LedgerDocumentIdentity(
                Guid.CreateVersion7(), "GATEWAY_PAYMENT", "pi_3", "INV-2026-00003",
                "lhdn-uuid-3", "INV-2026-00003", "B2B", "VALID", 108m, "MYR", DateTime.UtcNow));
        billing.FindLedgerByReferenceAsync(orgId, "GATEWAY_REFUND", paymentId.ToString("N") + ":" + eventId.ToString("N"))
            .Returns(new LedgerDocumentIdentity(
                Guid.CreateVersion7(), "GATEWAY_REFUND", "ref", "CN-2026-00010",
                null, "CN-2026-00010", "B2B", null, 108m, "MYR", DateTime.UtcNow));
        repo.GetTaxDocumentByInternalIdAsync(orgId, "INV-2026-00003").Returns(doc);
        lookup.GetCustomerForDocumentAsync(orgId, "pi_3", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new CommerceCustomerDisplay("Buyer Co", "buyer@example.com", "C98765432109"));
        crm.GetClientProfileByEmailAsync(orgId, "buyer@example.com")
            .Returns(new ClientProfileDto
            {
                Id = Guid.CreateVersion7().ToString(),
                Full_name = "Buyer Co",
                Email = "buyer@example.com",
                Phone = "60123456789",
                Tin = "C98765432109",
                Id_type = "BRN",
                Id_value = "202001099999",
                Consented_to_marketing = false
            });

        var @event = new GatewayRefundCompletedIntegrationEvent(
            orgId, Guid.Empty, paymentId, "pi_3", 108m, "MYR", 0m, 100m, 8m, IsFullRefund: true)
        {
            Id = eventId
        };

        await handler.HandleAsync(@event);

        await mediator.Received(1).Send(
            Arg.Is<SubmitTaxDocumentCommand>(c =>
                c.Payload.Document_type == SubmitDocumentRequestDtoDocument_type._02
                && c.Payload.Total_excluding_tax == 100
                && c.Payload.Total_tax == 8
                && c.Payload.Total_including_tax == 108
                && c.Payload.Items![0].Unit_price == 100
                && c.Payload.Items[0].Tax_rate == 8
                && c.Payload.Items[0].Tax_type_code == LhdnItemDtoTax_type_code._02),
            Arg.Any<CancellationToken>());
    }

    private static (LhdnRefundHandler Handler, ILhdnRepository Repo, IMediator Mediator, IBillingQueryService Billing)
        CreateHandler()
    {
        var full = CreateHandlerFull();
        return (full.Handler, full.Repo, full.Mediator, full.Billing);
    }

    private static (
        LhdnRefundHandler Handler,
        ILhdnRepository Repo,
        IMediator Mediator,
        IBillingQueryService Billing,
        ICrmQueryService Crm,
        ICommerceDocumentLookup Lookup) CreateHandlerFull()
    {
        var repo = Substitute.For<ILhdnRepository>();
        var mediator = Substitute.For<IMediator>();
        var billing = Substitute.For<IBillingQueryService>();
        var crm = Substitute.For<ICrmQueryService>();
        var lookup = Substitute.For<ICommerceDocumentLookup>();

        var handler = new LhdnRefundHandler(
            repo,
            mediator,
            billing,
            lookup,
            crm,
            NullLogger<LhdnRefundHandler>.Instance);
        return (handler, repo, mediator, billing, crm, lookup);
    }
}
