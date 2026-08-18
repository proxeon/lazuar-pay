using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lazuar.ApiTypes;
using MediatR;
using Modules.Billing.Contracts.Events;
using Modules.Lhdn.Application.Commands;
using Modules.Lhdn.Infrastructure.EventHandlers;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Lhdn;

[TestFixture]
public class ConsolidatedInvoiceIssuedIntegrationEventHandlerTests
{
    [Test]
    public async Task Submit_UsesStableIdempotencyKey()
    {
        var org = Guid.CreateVersion7();
        var mediator = Substitute.For<IMediator>();
        var handler = new ConsolidatedInvoiceIssuedIntegrationEventHandler(mediator);

        await handler.HandleAsync(new ConsolidatedInvoiceIssuedIntegrationEvent(
            org,
            "B2C-CONS-202607-" + org.ToString("N"),
            new DateTime(2026, 7, 28),
            new List<ConsolidatedLineItemDto>(),
            100m, 0m, 100m));

        await mediator.Received(1).Send(
            Arg.Is<SubmitTaxDocumentCommand>(c =>
                c.IdempotencyKey == $"b2c-cons:{org:N}:B2C-CONS-202607-{org:N}"
                && c.Payload.Internal_id.StartsWith("B2C-CONS-")
                && c.Payload.Document_type == SubmitDocumentRequestDtoDocument_type._01),
            Arg.Any<CancellationToken>());
    }
}
