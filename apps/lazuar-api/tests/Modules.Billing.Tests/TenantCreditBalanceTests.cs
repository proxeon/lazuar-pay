using System;
using BuildingBlocks.Domain;
using FluentAssertions;
using Modules.Billing.Domain.Aggregates;
using NUnit.Framework;

namespace Modules.Billing.Tests;

[TestFixture]
public class TenantCreditBalanceTests
{
    [Test]
    public void TopUp_IncreasesAvailableCredits()
    {
        var wallet = new TenantCreditBalance(Guid.NewGuid());
        wallet.TopUp(100, "test top-up");
        wallet.AvailableCredits.Should().Be(100);
    }

    [Test]
    public void Deduct_DecreasesAvailableCredits()
    {
        var wallet = new TenantCreditBalance(Guid.NewGuid());
        wallet.TopUp(100, "test top-up");
        wallet.Deduct(30, "email dispatch");
        wallet.AvailableCredits.Should().Be(70);
    }

    [Test]
    public void Deduct_ExactlyExhaustingBalance_Succeeds()
    {
        var wallet = new TenantCreditBalance(Guid.NewGuid());
        wallet.TopUp(50, "test top-up");
        wallet.Deduct(50, "email dispatch");
        wallet.AvailableCredits.Should().Be(0);
    }

    [Test]
    public void Deduct_ThrowsOnInsufficientBalance()
    {
        var wallet = new TenantCreditBalance(Guid.NewGuid());
        wallet.TopUp(10, "test top-up");
        var act = () => wallet.Deduct(30, "email dispatch");
        act.Should().Throw<BusinessRuleValidationException>();
    }

    [Test]
    public void Deduct_ThrowsWhenBalanceIsZero()
    {
        var wallet = new TenantCreditBalance(Guid.NewGuid());
        var act = () => wallet.Deduct(1, "email dispatch");
        act.Should().Throw<BusinessRuleValidationException>();
    }

    [Test]
    public void Deduct_ThrowsOnNonPositiveAmount()
    {
        var wallet = new TenantCreditBalance(Guid.NewGuid());
        wallet.TopUp(100, "test top-up");
        var act = () => wallet.Deduct(0, "email dispatch");
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Deduct_AppendsCreditLedgerTransaction()
    {
        var wallet = new TenantCreditBalance(Guid.NewGuid());
        wallet.TopUp(100, "test top-up");
        wallet.Deduct(25, "email dispatch");
        wallet.Transactions.Should().HaveCount(2);
    }
}
