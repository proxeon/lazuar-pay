// Lazuar.ModuleTests/Billing/EventHandlers/ManualSubscriberEnrolledHandlerTests.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MediatR;
using Modules.Billing.Application;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Infrastructure.EventHandlers;
using Modules.Commerce.Contracts.Events;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Billing.EventHandlers;

[TestFixture]
public class ManualSubscriberEnrolledHandlerTests
{
    [Test]
    public async Task HandleAsync_SavesChangesBeforeGeneratingDocument()
    {
        var repository = Substitute.For<ILedgerRepository>();
        var mediator = Substitute.For<IMediator>();
        var handler = new ManualSubscriberEnrolledIntegrationEventHandler(repository, mediator);

        var @event = new ManualSubscriberEnrolledIntegrationEvent(
            OrganizationId: Guid.CreateVersion7(),
            SubscriptionId: Guid.CreateVersion7(),
            ClientProfileId: Guid.CreateVersion7(),
            ProductId: Guid.CreateVersion7(),
            AmountPaid: 150m,
            Currency: "MYR",
            PaymentMethod: "BANK_TRANSFER",
            ReferenceNumber: "REF-999"
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

        await mediator.Received().Send(
            Arg.Is<GenerateAndStoreDocumentCommand>(c => c.CorrelationId == @event.SubscriptionId.ToString()),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task HandleAsync_TwoEventsSameSubscription_DifferentTransactionLogIds_BothBook()
    {
        var repository = Substitute.For<ILedgerRepository>();
        var mediator = Substitute.For<IMediator>();
        var handler = new ManualSubscriberEnrolledIntegrationEventHandler(repository, mediator);
        var processed = new HashSet<string>();
        var added = new List<LedgerEntry>();

        repository.HasEntryBeenProcessedAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(ci => processed.Contains(ci.ArgAt<string>(1)));
        repository.When(r => r.Add(Arg.Any<LedgerEntry>())).Do(ci =>
        {
            var entry = ci.Arg<LedgerEntry>();
            added.Add(entry);
            processed.Add(entry.ReferenceId);
        });
        mediator.Send(Arg.Any<GenerateNextSequenceNumberCommand>(), Arg.Any<CancellationToken>())
            .Returns("RCPT-2026");

        var subscriptionId = Guid.CreateVersion7();
        var firstLog = Guid.CreateVersion7();
        var secondLog = Guid.CreateVersion7();

        await handler.HandleAsync(Event(subscriptionId, firstLog));
        await handler.HandleAsync(Event(subscriptionId, secondLog));

        added.Should().HaveCount(2);
        added[0].ReferenceId.Should().Be(firstLog.ToString());
        added[1].ReferenceId.Should().Be(secondLog.ToString());
        await repository.Received(1).HasEntryBeenProcessedAsync(
            LedgerReferenceTypes.ManualEnrollment,
            firstLog.ToString());
        await repository.Received(1).HasEntryBeenProcessedAsync(
            LedgerReferenceTypes.ManualEnrollment,
            secondLog.ToString());
    }

    [Test]
    public async Task HandleAsync_ReplaySameTransactionLogId_DoesNotAddTwice()
    {
        var repository = Substitute.For<ILedgerRepository>();
        var mediator = Substitute.For<IMediator>();
        var handler = new ManualSubscriberEnrolledIntegrationEventHandler(repository, mediator);
        var logId = Guid.CreateVersion7();

        repository.HasEntryBeenProcessedAsync(LedgerReferenceTypes.ManualEnrollment, logId.ToString())
            .Returns(false, true);
        mediator.Send(Arg.Any<GenerateNextSequenceNumberCommand>(), Arg.Any<CancellationToken>())
            .Returns("RCPT-2026");

        var @event = Event(Guid.CreateVersion7(), logId);
        await handler.HandleAsync(@event);
        await handler.HandleAsync(@event);

        repository.Received(1).Add(Arg.Any<LedgerEntry>());
    }

    private static ManualSubscriberEnrolledIntegrationEvent Event(Guid subscriptionId, Guid transactionLogId) =>
        new(
            OrganizationId: Guid.CreateVersion7(),
            SubscriptionId: subscriptionId,
            ClientProfileId: Guid.CreateVersion7(),
            ProductId: Guid.CreateVersion7(),
            AmountPaid: 150m,
            Currency: "MYR",
            PaymentMethod: "BANK_TRANSFER",
            ReferenceNumber: "REF-999",
            TransactionLogId: transactionLogId);
}
