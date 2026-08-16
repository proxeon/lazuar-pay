using System;
using System.Threading;
using System.Threading.Tasks;
using Lazuar.ApiTypes;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Billing.Contracts.Events;
using Modules.Commerce.Contracts;
using Modules.CRM.Contracts;
using Modules.Lhdn.Application.Commands;
using Modules.Lhdn.Infrastructure.EventHandlers;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Lhdn;

[TestFixture]
public class B2bTaxInvoiceRequestedIntegrationEventHandlerTests
{
    [Test]
    public async Task MissingTin_DoesNotSubmit()
    {
        var orgId = Guid.CreateVersion7();
        var mediator = Substitute.For<IMediator>();
        var lookup = Substitute.For<ICommerceDocumentLookup>();
        var crm = Substitute.For<ICrmQueryService>();
        lookup.GetCustomerForDocumentAsync(orgId, "pi_1", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new CommerceCustomerDisplay("Buyer", "buyer@example.com"));

        var handler = new B2bTaxInvoiceRequestedIntegrationEventHandler(
            mediator, lookup, crm, NullLogger<B2bTaxInvoiceRequestedIntegrationEventHandler>.Instance);

        await handler.HandleAsync(new B2bTaxInvoiceRequestedIntegrationEvent(
            orgId, Guid.CreateVersion7(), "INV-2026-00001", "pi_1", 100m, 0m, "MYR"));

        await mediator.DidNotReceive().Send(Arg.Any<SubmitTaxDocumentCommand>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task StubTin_DoesNotSubmit()
    {
        var orgId = Guid.CreateVersion7();
        var mediator = Substitute.For<IMediator>();
        var lookup = Substitute.For<ICommerceDocumentLookup>();
        var crm = Substitute.For<ICrmQueryService>();
        lookup.GetCustomerForDocumentAsync(orgId, "pi_1", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new CommerceCustomerDisplay("Buyer", "buyer@example.com", "C1234567890"));
        crm.GetClientProfileByEmailAsync(orgId, "buyer@example.com")
            .Returns(new ClientProfileDto
            {
                Id = Guid.CreateVersion7().ToString(),
                Full_name = "Buyer",
                Email = "buyer@example.com",
                Phone = "",
                Tin = "C1234567890",
                Consented_to_marketing = false
            });

        var handler = new B2bTaxInvoiceRequestedIntegrationEventHandler(
            mediator, lookup, crm, NullLogger<B2bTaxInvoiceRequestedIntegrationEventHandler>.Instance);

        await handler.HandleAsync(new B2bTaxInvoiceRequestedIntegrationEvent(
            orgId, Guid.CreateVersion7(), "INV-2026-00001", "pi_1", 100m, 0m, "MYR"));

        await mediator.DidNotReceive().Send(Arg.Any<SubmitTaxDocumentCommand>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RealTin_SubmitsType01WithInvoiceNumber()
    {
        var orgId = Guid.CreateVersion7();
        var mediator = Substitute.For<IMediator>();
        var lookup = Substitute.For<ICommerceDocumentLookup>();
        var crm = Substitute.For<ICrmQueryService>();
        lookup.GetCustomerForDocumentAsync(orgId, "pi_1", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new CommerceCustomerDisplay("Buyer Co", "buyer@example.com", "C55555555555"));
        crm.GetClientProfileByEmailAsync(orgId, "buyer@example.com")
            .Returns(new ClientProfileDto
            {
                Id = Guid.CreateVersion7().ToString(),
                Full_name = "Buyer Co",
                Email = "buyer@example.com",
                Phone = "60111",
                Tin = "C55555555555",
                Id_type = "BRN",
                Id_value = "202001012345",
                Consented_to_marketing = false
            });

        var handler = new B2bTaxInvoiceRequestedIntegrationEventHandler(
            mediator, lookup, crm, NullLogger<B2bTaxInvoiceRequestedIntegrationEventHandler>.Instance);

        await handler.HandleAsync(new B2bTaxInvoiceRequestedIntegrationEvent(
            orgId, Guid.CreateVersion7(), "INV-2026-00008", "pi_1", 200m, 16m, "MYR"));

        await mediator.Received(1).Send(
            Arg.Is<SubmitTaxDocumentCommand>(c =>
                c.Payload.Document_type == SubmitDocumentRequestDtoDocument_type._01
                && c.Payload.Internal_id == "INV-2026-00008"
                && c.Payload.Buyer_tin == "C55555555555"
                && c.Payload.Total_excluding_tax == 200),
            Arg.Any<CancellationToken>());
    }
}
