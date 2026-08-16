using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using FluentAssertions;
using Lazuar.ApiTypes;
using MediatR;
using Modules.Commerce.Application;
using Modules.Commerce.Application.Commands;
using Modules.Commerce.Contracts.Commands;
using Modules.Commerce.Contracts.Events;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.Entities;
using Modules.Commerce.Domain.ValueObjects;
using Modules.CRM.Contracts;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class CreateManualSubscriberCommandHandlerTests
{
    [Test]
    public async Task C1_RecurringBankTransfer_EnrollsReminderOnly_WritesLogAndLedger()
    {
        var fx = await ActAsync(amount: 150m, method: "BANK_TRANSFER", welcome: true);

        fx.Subscription.Should().NotBeNull();
        fx.Subscription!.Status.Should().Be("ACTIVE");
        fx.Subscription.IsReminderOnly.Should().BeTrue();
        fx.Subscription.CurrentPeriodEnd.Should().Be(fx.Start);
        fx.Subscription.NextBillingDate.Should().BeCloseTo(fx.Start.AddMonths(1), TimeSpan.FromSeconds(1));
        fx.Logs.Should().HaveCount(1);
        fx.Logs[0].Amount.Should().Be(150m);
        fx.Logs[0].RecordedByName.Should().Be("BANK_TRANSFER");
        fx.Logs[0].SubscriptionId.Should().Be(fx.Subscription.Id);
        fx.LedgerEvents.Should().HaveCount(1);
        fx.LedgerEvents[0].TransactionLogId.Should().Be(fx.Logs[0].Id);
        fx.ActivatedEvents.Should().HaveCount(1);
        fx.ActivatedEvents[0].IsFirstPayment.Should().BeTrue();
    }

    [Test]
    public async Task C2_WelcomeFalse_DoesNotPublishActivated()
    {
        var fx = await ActAsync(amount: 150m, method: "BANK_TRANSFER", welcome: false);

        fx.ActivatedEvents.Should().BeEmpty();
        fx.LedgerEvents.Should().HaveCount(1);
    }

    [Test]
    public async Task C3_Comped_ForcesZero_NoLedger_StillSetsDueDate()
    {
        var fx = await ActAsync(amount: 99m, method: "COMPED", welcome: false);

        fx.Logs.Should().HaveCount(1);
        fx.Logs[0].Amount.Should().Be(0m);
        fx.Logs[0].RecordedByName.Should().Be("COMPED");
        fx.LedgerEvents.Should().BeEmpty();
        fx.Subscription!.NextBillingDate.Should().NotBeNull();
    }

    [Test]
    public async Task C4_OneTime_Throws_NoSubscription()
    {
        var act = async () => await ActAsync(amount: 150m, method: "BANK_TRANSFER", interval: "one_time");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*recurring*");
    }

    [Test]
    public async Task C5_ArchivedProduct_Throws()
    {
        var act = async () => await ActAsync(amount: 150m, method: "BANK_TRANSFER", archived: true);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*archived*");
    }

    [Test]
    public async Task C6_WrongOrgProduct_Throws()
    {
        var act = async () => await ActAsync(amount: 150m, method: "BANK_TRANSFER", foreignProduct: true);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }

    [Test]
    public async Task C7_SecondActiveSameClientProduct_Throws()
    {
        var act = async () => await ActAsync(amount: 150m, method: "BANK_TRANSFER", alreadyActive: true);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exists*");
    }

    [Test]
    public async Task C8_NextBillingOverride_UsesThatDate_PeriodEndIsStart()
    {
        var start = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var next = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var fx = await ActAsync(amount: 150m, method: "BANK_TRANSFER", start: start, nextBilling: next);

        fx.Subscription!.CurrentPeriodEnd.Should().Be(start);
        fx.Subscription.NextBillingDate.Should().Be(next);
    }

    [Test]
    public async Task C9_YearlyProduct_AddsOneYear()
    {
        var start = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var fx = await ActAsync(amount: 150m, method: "BANK_TRANSFER", interval: "yr", start: start);

        fx.Subscription!.NextBillingDate.Should().Be(start.AddYears(1));
    }

    [Test]
    public async Task C10_LowercaseComped_TreatedAsComped()
    {
        var fx = await ActAsync(amount: 99m, method: "comped");

        fx.Logs[0].Amount.Should().Be(0m);
        fx.Logs[0].RecordedByName.Should().Be("COMPED");
        fx.LedgerEvents.Should().BeEmpty();
    }

    [Test]
    public async Task C11_ZeroAmountBankTransfer_Throws()
    {
        var act = async () => await ActAsync(amount: 0m, method: "BANK_TRANSFER");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*greater than zero*");
    }

    [Test]
    public async Task C12_NegativeAmount_Throws()
    {
        var act = async () => await ActAsync(amount: -1m, method: "CASH");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*negative*");
    }

    private static async Task<CreateFx> ActAsync(
        decimal amount,
        string method,
        bool welcome = false,
        string interval = "mo",
        bool archived = false,
        bool foreignProduct = false,
        bool alreadyActive = false,
        DateTime? start = null,
        DateTime? nextBilling = null)
    {
        var orgId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var productOrg = foreignProduct ? Guid.CreateVersion7() : orgId;
        var product = new Product(
            productOrg,
            "Pro Plan",
            "pro-plan",
            100m,
            "FIXED",
            0m,
            "MYR",
            interval,
            "STRIPE",
            new CheckoutConfiguration(false, false, false),
            new[] { "telegram" });
        if (archived)
        {
            product.Archive();
        }

        var subscriptions = new List<Subscription>();
        var logs = new List<CommerceTransactionLog>();
        var repository = Substitute.For<ICommerceRepository>();
        repository.GetProductByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);
        repository.HasActiveSubscriptionAsync(orgId, clientId, product.Id, Arg.Any<CancellationToken>())
            .Returns(alreadyActive);
        repository.When(r => r.AddSubscription(Arg.Any<Subscription>())).Do(ci => subscriptions.Add(ci.Arg<Subscription>()));
        repository.When(r => r.AddTransactionLog(Arg.Any<CommerceTransactionLog>())).Do(ci => logs.Add(ci.Arg<CommerceTransactionLog>()));

        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<ResolveClientProfileCommand>(), Arg.Any<CancellationToken>()).Returns(clientId);

        var ledgerEvents = new List<ManualSubscriberEnrolledIntegrationEvent>();
        var activatedEvents = new List<SubscriptionActivatedIntegrationEvent>();
        var eventBus = Substitute.For<IEventBus>();
        eventBus.PublishAsync(Arg.Do<ManualSubscriberEnrolledIntegrationEvent>(ledgerEvents.Add))
            .Returns(Task.CompletedTask);
        eventBus.PublishAsync(Arg.Do<SubscriptionActivatedIntegrationEvent>(activatedEvents.Add))
            .Returns(Task.CompletedTask);

        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(clientId).Returns(new ClientProfileDto
        {
            Id = clientId.ToString(),
            Full_name = "Ahmad Ali",
            Email = "ahmad@example.com"
        });

        var startDate = start ?? new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc);
        var handler = new CreateManualSubscriberCommandHandler(repository, mediator, eventBus, crm);
        await handler.Handle(new CreateManualSubscriberCommand(
            orgId,
            "Ahmad Ali",
            "ahmad@example.com",
            "+60123456789",
            product.Id,
            method,
            amount,
            "REF-1",
            welcome,
            startDate,
            nextBilling), CancellationToken.None);

        return new CreateFx(
            startDate,
            subscriptions.Count == 0 ? null : subscriptions[0],
            logs,
            ledgerEvents,
            activatedEvents);
    }

    private sealed record CreateFx(
        DateTime Start,
        Subscription? Subscription,
        List<CommerceTransactionLog> Logs,
        List<ManualSubscriberEnrolledIntegrationEvent> LedgerEvents,
        List<SubscriptionActivatedIntegrationEvent> ActivatedEvents);
}
