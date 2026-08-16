using System;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Billing.Domain;

[TestFixture]
public class WorkspaceSaasSubscriptionTests
{
    [Test]
    public void NewRow_IsUnpaid_WithoutPeriod()
    {
        var org = Guid.CreateVersion7();
        var sub = new WorkspaceSaasSubscription(org, "hub_starter");

        Assert.That(sub.Status, Is.EqualTo(WorkspaceSaasStatuses.Unpaid));
        Assert.That(sub.CurrentPeriodStart, Is.Null);
        Assert.That(sub.CurrentPeriodEnd, Is.Null);
        Assert.That(sub.OrganizationId, Is.EqualTo(org));
    }

    [Test]
    public void ActivateFromPayment_FirstPay_StartsFromNow()
    {
        var sub = new WorkspaceSaasSubscription(Guid.CreateVersion7(), "hub_starter");
        var now = new DateTime(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc);

        sub.ActivateFromPayment(now, SaasPlanInterval.Month, "tx_1");

        Assert.That(sub.Status, Is.EqualTo(WorkspaceSaasStatuses.Active));
        Assert.That(sub.CurrentPeriodStart, Is.EqualTo(now));
        Assert.That(sub.CurrentPeriodEnd, Is.EqualTo(now.AddMonths(1)));
        Assert.That(sub.NextInvoiceAt, Is.EqualTo(now.AddMonths(1)));
        Assert.That(sub.LastGatewayTransactionId, Is.EqualTo("tx_1"));
    }

    [Test]
    public void ActivateFromPayment_WhileActive_ExtendsFromCurrentEnd()
    {
        var sub = new WorkspaceSaasSubscription(Guid.CreateVersion7(), "hub_starter");
        var start = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        sub.ActivateFromPayment(start, SaasPlanInterval.Month, "tx_1");

        var renewAt = start.AddDays(10);
        sub.ActivateFromPayment(renewAt, SaasPlanInterval.Month, "tx_2");

        Assert.That(sub.CurrentPeriodStart, Is.EqualTo(start.AddMonths(1)));
        Assert.That(sub.CurrentPeriodEnd, Is.EqualTo(start.AddMonths(2)));
        Assert.That(sub.LastGatewayTransactionId, Is.EqualTo("tx_2"));
    }

    [Test]
    public void ActivateFromPayment_AfterPeriodEnded_StartsFromNow()
    {
        var sub = new WorkspaceSaasSubscription(Guid.CreateVersion7(), "hub_starter");
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        sub.ActivateFromPayment(start, SaasPlanInterval.Month, "tx_1");

        var later = start.AddMonths(2);
        sub.ActivateFromPayment(later, SaasPlanInterval.Year, "tx_2");

        Assert.That(sub.CurrentPeriodStart, Is.EqualTo(later));
        Assert.That(sub.CurrentPeriodEnd, Is.EqualTo(later.AddYears(1)));
    }

    [Test]
    public void MarkPastDue_And_Cancel()
    {
        var sub = new WorkspaceSaasSubscription(Guid.CreateVersion7(), "hub_starter");
        sub.ActivateFromPayment(DateTime.UtcNow, SaasPlanInterval.Month);
        sub.MarkPastDue();
        Assert.That(sub.Status, Is.EqualTo(WorkspaceSaasStatuses.PastDue));
        sub.Cancel();
        Assert.That(sub.Status, Is.EqualTo(WorkspaceSaasStatuses.Canceled));
    }
}
