using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Modules.Billing.Application;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Infrastructure.EventHandlers;
using Modules.Payments.Contracts.Events;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Billing.EventHandlers;

[TestFixture]
public class GatewayPaymentCompletedHandlerTests
{
    [Test]
    public async Task HandleAsync_WhenB2C_SavesChangesBeforeGeneratingDocument()
    {
        var repository = Substitute.For<ILedgerRepository>();
        var mediator = Substitute.For<IMediator>();
        var handler = new GatewayPaymentCompletedHandler(repository, mediator);

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

        // Asserts that database changes are strictly committed before the document generation 
        // command is dispatched, preventing "Record Not Found" query errors downstream.
        Received.InOrder(() =>
        {
            mediator.Send(Arg.Any<GenerateNextSequenceNumberCommand>(), Arg.Any<CancellationToken>());
            repository.SaveChangesAsync(Arg.Any<CancellationToken>());
            mediator.Send(Arg.Any<GenerateAndStoreDocumentCommand>(), Arg.Any<CancellationToken>());
        });
    }
}
