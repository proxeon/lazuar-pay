using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using FluentAssertions;
using Lazuar.ApiTypes;
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
public class RecordSubscriberPaymentCommandHandlerTests
{
    [Test]
    public async Task R1_ActivePaid_AdvancesFromNow_NoActivated_NoRecovery()
    {
        var fx = await ActAsync(status: "ACTIVE", amount: 100m, method: "BANK_TRANSFER");

        fx.Subscription.Status.Should().Be("ACTIVE");
        fx.Subscription.CurrentPeriodEnd.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        fx.Subscription.NextBillingDate.Should().BeCloseTo(DateTime.UtcNow.AddMonths(1), TimeSpan.FromSeconds(5));
        fx.Logs.Should().HaveCount(1);
        fx.LedgerEvents.Should().HaveCount(1);
        fx.LedgerEvents[0].TransactionLogId.Should().Be(fx.Logs[0].Id);
        fx.ActivatedEvents.Should().BeEmpty();
        fx.ResumedEvents.Should().BeEmpty();
        fx.Campaign.RecoveredRevenue.Should().Be(0);
    }

    [Test]
    public async Task R2_TwoPaymentsNoClerkRef_TwoDistinctLedgerKeys()
    {
        var first = await ActAsync(status: "ACTIVE", amount: 100m, method: "CASH", reference: null);
        var second = await ActAsync(
            status: "ACTIVE",
            amount: 100m,
            method: "CASH",
            reference: null,
            existing: first);

        second.Logs.Should().HaveCount(2);
        second.LedgerEvents.Should().HaveCount(2);
        second.LedgerEvents[0].TransactionLogId.Should().NotBe(second.LedgerEvents[1].TransactionLogId);
        second.LedgerEvents.Select(e => e.TransactionLogId).Should().BeEquivalentTo(second.Logs.Select(l => l.Id));
    }

    [Test]
    public async Task R3_SameReferenceTwice_IsIdempotent()
    {
        var first = await ActAsync(status: "ACTIVE", amount: 100m, method: "BANK_TRANSFER", reference: "DUITNOW-1");
        var firstEnd = first.Subscription.CurrentPeriodEnd;
        var firstNext = first.Subscription.NextBillingDate;

        var second = await ActAsync(
            status: "ACTIVE",
            amount: 100m,
            method: "BANK_TRANSFER",
            reference: "DUITNOW-1",
            existing: first);

        second.Logs.Should().HaveCount(1);
        second.LedgerEvents.Should().HaveCount(1);
        second.Subscription.CurrentPeriodEnd.Should().Be(firstEnd);
        second.Subscription.NextBillingDate.Should().Be(firstNext);
    }

    [Test]
    public async Task R4_PastDue_RecoversAndActivates()
    {
        var fx = await ActAsync(status: "PAST_DUE", amount: 100m, method: "BANK_TRANSFER");

        fx.Subscription.Status.Should().Be("ACTIVE");
        fx.Subscription.CurrentDunningCampaignId.Should().BeNull();
        fx.Campaign.RecoveredRevenue.Should().Be(100m);
        fx.ActivatedEvents.Should().HaveCount(1);
        fx.ActivatedEvents[0].IsFirstPayment.Should().BeFalse();
        fx.ResumedEvents.Should().BeEmpty();
    }

    [Test]
    public async Task R5_Suspended_MovesPeriodEnd_PublishesResumedOnly()
    {
        var fx = await ActAsync(status: "SUSPENDED", amount: 100m, method: "BANK_TRANSFER");

        fx.Subscription.Status.Should().Be("ACTIVE");
        fx.Subscription.CurrentPeriodEnd.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        fx.Subscription.NextBillingDate.Should().BeCloseTo(DateTime.UtcNow.AddMonths(1), TimeSpan.FromSeconds(5));
        fx.ResumedEvents.Should().HaveCount(1);
        fx.ActivatedEvents.Should().BeEmpty();
    }

    [Test]
    public async Task R6_CompedFromPastDue_ActivatesWithoutLedgerOrRecovery()
    {
        var fx = await ActAsync(status: "PAST_DUE", amount: 100m, method: "COMPED");

        fx.Subscription.Status.Should().Be("ACTIVE");
        fx.Logs[0].Amount.Should().Be(0m);
        fx.LedgerEvents.Should().BeEmpty();
        fx.Campaign.RecoveredRevenue.Should().Be(0);
        fx.ActivatedEvents.Should().HaveCount(1);
        fx.ActivatedEvents[0].IsFirstPayment.Should().BeFalse();
    }

    [Test]
    public async Task R7_CompedFromActive_NoActivatedNoLedger()
    {
        var fx = await ActAsync(status: "ACTIVE", amount: 0m, method: "COMPED");

        fx.ActivatedEvents.Should().BeEmpty();
        fx.LedgerEvents.Should().BeEmpty();
        fx.Logs[0].Amount.Should().Be(0m);
    }

    [TestCase("PENDING")]
    [TestCase("CANCELED")]
    public async Task R8_TerminalOrPending_Throws(string status)
    {
        var act = async () => await ActAsync(status: status, amount: 100m, method: "CASH");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*status*");
    }

    [Test]
    public async Task R8_OneTime_Throws()
    {
        var act = async () => await ActAsync(status: "ACTIVE", amount: 100m, method: "CASH", interval: "one_time");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*recurring*");
    }

    [Test]
    public async Task R8_Negative_Throws()
    {
        var act = async () => await ActAsync(status: "ACTIVE", amount: -5m, method: "CASH");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*negative*");
    }

    [Test]
    public async Task R9_NextBillingOverride_UsesThatDate()
    {
        var next = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var fx = await ActAsync(status: "ACTIVE", amount: 100m, method: "BANK_TRANSFER", nextBilling: next);

        fx.Subscription.NextBillingDate.Should().Be(next);
        fx.Subscription.CurrentPeriodEnd.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task R10_ReminderOnlyPreserved()
    {
        var fx = await ActAsync(status: "ACTIVE", amount: 100m, method: "BANK_TRANSFER", reminderOnly: true);

        fx.Subscription.IsReminderOnly.Should().BeTrue();
    }

    [Test]
    public async Task YearlyBillingInterval_AdvancesOneYear_NotCatalogMonth()
    {
        var fx = await ActAsync(
            status: "ACTIVE",
            amount: 500m,
            method: "BANK_TRANSFER",
            interval: "mo",
            billingInterval: "yr");

        fx.Subscription.NextBillingDate.Should().BeCloseTo(DateTime.UtcNow.AddYears(1), TimeSpan.FromSeconds(5));
    }

    private static async Task<RecordFx> ActAsync(
        string status,
        decimal amount,
        string method,
        string? reference = "TRX-1",
        string interval = "mo",
        DateTime? nextBilling = null,
        bool reminderOnly = true,
        string? billingInterval = null,
        RecordFx? existing = null)
    {
        if (existing != null)
        {
            await existing.Handler.Handle(
                new RecordSubscriberPaymentCommand(
                    existing.OrgId,
                    existing.Subscription.Id,
                    amount,
                    method,
                    reference,
                    nextBilling),
                CancellationToken.None);
            return existing;
        }

        var orgId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var product = new Product(
            orgId,
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
        var campaign = new DunningCampaign(orgId, "Default recovery", "SUSPEND", 7);
        var sub = new Subscription(orgId, clientId, product.Id);
        if (status != "PENDING")
        {
            sub.Activate(DateTime.UtcNow.AddDays(-40), DateTime.UtcNow.AddDays(-10), reminderOnly);
        }

        if (status == "PAST_DUE")
        {
            sub.MarkAsPastDue();
            sub.AssignDunningCampaign(campaign.Id);
        }
        else if (status == "SUSPENDED")
        {
            sub.MarkAsPastDue();
            sub.AssignDunningCampaign(campaign.Id);
            sub.Suspend();
        }
        else if (status == "CANCELED")
        {
            sub.Cancel();
        }
        else if (status == "ACTIVE")
        {
            sub.AssignDunningCampaign(campaign.Id);
        }

        if (!string.IsNullOrWhiteSpace(billingInterval))
        {
            sub.SetBillingInterval(billingInterval);
        }

        var logs = new List<CommerceTransactionLog>();
        var ledgerEvents = new List<ManualSubscriberEnrolledIntegrationEvent>();
        var activatedEvents = new List<SubscriptionActivatedIntegrationEvent>();
        var resumedEvents = new List<SubscriptionResumedIntegrationEvent>();
        var repository = Substitute.For<ICommerceRepository>();
        repository.GetSubscriptionByIdAsync(Arg.Any<Guid>(), sub.Id, Arg.Any<CancellationToken>()).Returns(sub);
        repository.GetProductByIdAsync(Arg.Any<Guid>(), product.Id, Arg.Any<CancellationToken>()).Returns(product);
        repository.GetDunningCampaignByIdAsync(orgId, campaign.Id, Arg.Any<CancellationToken>()).Returns(campaign);
        repository.GetConfirmedTransactionLogByReferenceAsync(
                orgId, sub.Id, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var wanted = ci.ArgAt<string>(2);
                return logs.FirstOrDefault(l =>
                    l.ExternalReference == wanted && l.Status == CommerceTransactionLog.StatusConfirmed);
            });
        repository.When(r => r.AddTransactionLog(Arg.Any<CommerceTransactionLog>()))
            .Do(ci => logs.Add(ci.Arg<CommerceTransactionLog>()));

        var eventBus = Substitute.For<IEventBus>();
        eventBus.PublishAsync(Arg.Do<ManualSubscriberEnrolledIntegrationEvent>(ledgerEvents.Add))
            .Returns(Task.CompletedTask);
        eventBus.PublishAsync(Arg.Do<SubscriptionActivatedIntegrationEvent>(activatedEvents.Add))
            .Returns(Task.CompletedTask);
        eventBus.PublishAsync(Arg.Do<SubscriptionResumedIntegrationEvent>(resumedEvents.Add))
            .Returns(Task.CompletedTask);

        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(clientId).Returns(new ClientProfileDto
        {
            Id = clientId.ToString(),
            Full_name = "Past Due User",
            Email = "pastdue@example.com"
        });

        var handler = new RecordSubscriberPaymentCommandHandler(repository, eventBus, crm);
        await handler.Handle(
            new RecordSubscriberPaymentCommand(orgId, sub.Id, amount, method, reference, nextBilling),
            CancellationToken.None);

        return new RecordFx(orgId, sub, campaign, logs, ledgerEvents, activatedEvents, resumedEvents, handler);
    }

    private sealed class RecordFx
    {
        public RecordFx(
            Guid orgId,
            Subscription subscription,
            DunningCampaign campaign,
            List<CommerceTransactionLog> logs,
            List<ManualSubscriberEnrolledIntegrationEvent> ledgerEvents,
            List<SubscriptionActivatedIntegrationEvent> activatedEvents,
            List<SubscriptionResumedIntegrationEvent> resumedEvents,
            RecordSubscriberPaymentCommandHandler handler)
        {
            OrgId = orgId;
            Subscription = subscription;
            Campaign = campaign;
            Logs = logs;
            LedgerEvents = ledgerEvents;
            ActivatedEvents = activatedEvents;
            ResumedEvents = resumedEvents;
            Handler = handler;
        }

        public Guid OrgId { get; }
        public Subscription Subscription { get; }
        public DunningCampaign Campaign { get; }
        public List<CommerceTransactionLog> Logs { get; }
        public List<ManualSubscriberEnrolledIntegrationEvent> LedgerEvents { get; }
        public List<SubscriptionActivatedIntegrationEvent> ActivatedEvents { get; }
        public List<SubscriptionResumedIntegrationEvent> ResumedEvents { get; }
        public RecordSubscriberPaymentCommandHandler Handler { get; }
    }
}
