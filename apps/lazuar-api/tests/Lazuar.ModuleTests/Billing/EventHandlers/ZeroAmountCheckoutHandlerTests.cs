using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Modules.Billing.Application;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Infrastructure.EventHandlers;
using Modules.Commerce.Contracts.Events;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Billing.EventHandlers;

[TestFixture]
public class ZeroAmountCheckoutHandlerTests
{
    [Test]
    public async Task TrialWithListPriceAndZeroDiscount_Balances()
    {
        var org = Guid.CreateVersion7();
        var sessionId = Guid.CreateVersion7();
        LedgerEntry? added = null;
        var repo = Substitute.For<ILedgerRepository>();
        repo.HasEntryBeenProcessedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        repo.When(r => r.Add(Arg.Any<LedgerEntry>())).Do(ci => added = ci.Arg<LedgerEntry>());

        var handler = new ZeroAmountCheckoutHandler(repo);
        await handler.HandleAsync(new ZeroAmountCheckoutCompletedIntegrationEvent(
            org,
            sessionId,
            Guid.CreateVersion7(),
            OriginalAmount: 150m,
            DiscountAmount: 0m,
            Currency: "MYR",
            CouponCode: "NONE",
            Metadata: new Dictionary<string, string>()));

        Assert.That(added, Is.Not.Null);
        Assert.That(added!.Lines.Sum(l => l.Amount), Is.EqualTo(0m));
        Assert.That(
            added.Lines.Single(l => l.AccountType == AccountTypes.ExpenseDiscount).Amount,
            Is.EqualTo(150m));
        Assert.That(
            added.Lines.Single(l => l.AccountType == AccountTypes.RevenueGross).Amount,
            Is.EqualTo(-150m));
    }
}
