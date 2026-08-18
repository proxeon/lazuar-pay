using System;
using FluentAssertions;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Billing.Domain;

[TestFixture]
public class LedgerEntryBalanceTests
{
    [Test]
    public void ValidateBalanced_EmptyLines_Throws()
    {
        var entry = new LedgerEntry(Guid.CreateVersion7(), LedgerReferenceTypes.ZeroAmountCheckout, "z0", "empty");
        var act = () => entry.ValidateBalanced();
        act.Should().Throw<InvalidOperationException>().WithMessage("*no lines*");
    }

    [Test]
    public void ValidateBalanced_CrossCurrencyBaseCancel_Throws()
    {
        var entry = new LedgerEntry(Guid.CreateVersion7(), LedgerReferenceTypes.GatewayPayment, "tx-fx", "mixed");
        entry.AddLine(AccountTypes.AssetCash, 100m, "USD", 100m, "USD");
        entry.AddLine(AccountTypes.RevenueGross, -100m, "MYR", -100m, "MYR");
        var act = () => entry.ValidateBalanced();
        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void ValidateBalanced_NativeAmountMismatch_Throws()
    {
        var entry = new LedgerEntry(Guid.CreateVersion7(), LedgerReferenceTypes.GatewayPayment, "tx-fx2", "fx");
        entry.AddLine(AccountTypes.AssetCash, 100m, "USD", 450m, "MYR");
        entry.AddLine(AccountTypes.RevenueGross, -90m, "USD", -450m, "MYR");
        var act = () => entry.ValidateBalanced();
        act.Should().Throw<InvalidOperationException>().WithMessage("*USD*");
    }

    [Test]
    public void ValidateBalanced_BalancedMyrSale_Passes()
    {
        var entry = new LedgerEntry(Guid.CreateVersion7(), LedgerReferenceTypes.GatewayPayment, "tx-ok", "sale");
        entry.AddLine(AccountTypes.AssetCash, 92m, "MYR", 92m, "MYR");
        entry.AddLine(AccountTypes.ExpenseGatewayFee, 8m, "MYR", 8m, "MYR");
        entry.AddLine(AccountTypes.RevenueGross, -100m, "MYR", -100m, "MYR");
        entry.ValidateBalanced();
    }
}
