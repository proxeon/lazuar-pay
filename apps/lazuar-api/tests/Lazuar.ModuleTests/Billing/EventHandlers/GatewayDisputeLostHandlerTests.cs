using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Infrastructure;
using Lazuar.TestSupport;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Infrastructure;
using Modules.Billing.Infrastructure.EventHandlers;
using Modules.Billing.Infrastructure.Repositories;
using Modules.Payments.Contracts;
using Modules.Payments.Contracts.Events;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Billing.EventHandlers;

[TestFixture]
public class GatewayDisputeLostHandlerTests
{
    private BillingDbContext _db = null!;
    private GatewayDisputeLostHandler _handler = null!;
    private Guid _orgId;

    [SetUp]
    public void SetUp()
    {
        _orgId = Guid.CreateVersion7();
        _db = new BillingDbContext(
            InMemoryDb.CreateOptions<BillingDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());
        _handler = new GatewayDisputeLostHandler(_db, new LedgerRepository(_db));
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task Lost_ReversesOriginalSale()
    {
        await SeedSaleAsync("pi_lost", 100m);
        await _handler.HandleAsync(Closed("pi_lost", "lost"));

        var dispute = await _db.LedgerEntries.IgnoreQueryFilters().Include(e => e.Lines)
            .SingleAsync(e => e.ReferenceType == LedgerReferenceTypes.GatewayDispute);
        Assert.That(dispute.ReferenceId, Is.EqualTo("pi_lost"));
        Assert.That(dispute.Lines.Sum(l => l.Amount), Is.EqualTo(0m));
        Assert.That(
            dispute.Lines.Single(l => l.AccountType == AccountTypes.RevenueGross).Amount,
            Is.EqualTo(100m));
    }

    [Test]
    public async Task Won_DoesNotJournal()
    {
        await SeedSaleAsync("pi_won", 100m);
        await _handler.HandleAsync(Closed("pi_won", "won"));

        Assert.That(
            await _db.LedgerEntries.IgnoreQueryFilters()
                .CountAsync(e => e.ReferenceType == LedgerReferenceTypes.GatewayDispute),
            Is.EqualTo(0));
    }

    [Test]
    public async Task Lost_WhenAlreadyRefunded_DoesNotJournal()
    {
        await SeedSaleAsync("pi_rf", 100m);
        var refund = new LedgerEntry(_orgId, LedgerReferenceTypes.GatewayRefund, "r1",
            "Refund processed for subscription x (gateway tx pi_rf)");
        refund.AddLine(AccountTypes.AssetCash, -100m, "MYR", -100m, "MYR");
        refund.AddLine(AccountTypes.ContraRevenueRefunds, 100m, "MYR", 100m, "MYR");
        refund.ValidateBalanced();
        refund.MarkConsolidationNotRequired();
        _db.LedgerEntries.Add(refund);
        await _db.SaveChangesAsync();

        await _handler.HandleAsync(Closed("pi_rf", "lost"));

        Assert.That(
            await _db.LedgerEntries.IgnoreQueryFilters()
                .CountAsync(e => e.ReferenceType == LedgerReferenceTypes.GatewayDispute),
            Is.EqualTo(0));
    }

    [Test]
    public async Task Lost_UtilityTopUp_DoesNotJournalGmv()
    {
        await SeedSaleAsync("pi_util", 50m);
        await _handler.HandleAsync(Closed("pi_util", "lost", PlatformCheckoutTypes.UtilityCreditTopup));

        Assert.That(
            await _db.LedgerEntries.IgnoreQueryFilters()
                .CountAsync(e => e.ReferenceType == LedgerReferenceTypes.GatewayDispute),
            Is.EqualTo(0));
    }

    private async Task SeedSaleAsync(string tx, decimal amount)
    {
        var sale = new LedgerEntry(_orgId, LedgerReferenceTypes.GatewayPayment, tx, "sale", "B2C");
        sale.AddLine(AccountTypes.AssetCash, amount, "MYR", amount, "MYR");
        sale.AddLine(AccountTypes.RevenueGross, -amount, "MYR", -amount, "MYR");
        sale.ValidateBalanced();
        sale.AssignB2cReceipt("RCPT-1");
        _db.LedgerEntries.Add(sale);
        await _db.SaveChangesAsync();
    }

    private GatewayDisputeClosedIntegrationEvent Closed(string tx, string outcome, string? type = null) =>
        new(
            OrganizationId: _orgId,
            GatewayTransactionId: tx,
            Outcome: outcome,
            Metadata: type is null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["type"] = type });
}
