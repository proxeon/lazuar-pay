using System;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.Billing.Application;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Infrastructure;
using Modules.Billing.Infrastructure.EventHandlers;
using Modules.Billing.Infrastructure.Repositories;
using Modules.Payments.Contracts.Events;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Billing.EventHandlers;

[TestFixture]
public class GatewayRefundCompletedHandlerTests
{
    private BillingDbContext _db = null!;
    private ILedgerRepository _repo = null!;
    private GatewayRefundCompletedHandler _handler = null!;
    private Guid _orgId;

    [SetUp]
    public void SetUp()
    {
        _orgId = Guid.CreateVersion7();
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var ctx = Substitute.For<IExecutionContextAccessor>();
        ctx.TenantId.Returns(Guid.Empty);
        _db = new BillingDbContext(options, ctx, Substitute.For<IMediator>(), new DatabaseJobTrigger());
        _repo = new LedgerRepository(_db);
        _handler = new GatewayRefundCompletedHandler(_repo, _db);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private async Task SeedOriginalPaymentAsync(string gatewayTxId, decimal gross, decimal tax)
    {
        var entry = new LedgerEntry(_orgId, LedgerReferenceTypes.GatewayPayment, gatewayTxId, "sale", "B2C");
        entry.AddLine(AccountTypes.AssetCash, gross + tax, "MYR", gross + tax, "MYR");
        entry.AddLine(AccountTypes.RevenueGross, -gross, "MYR", -gross, "MYR");
        if (tax > 0)
            entry.AddLine(AccountTypes.LiabilityTaxPayable, -tax, "MYR", -tax, "MYR");
        entry.ValidateBalanced();
        entry.AssignB2cReceipt("RCPT-TEST-1");
        _db.LedgerEntries.Add(entry);
        await _db.SaveChangesAsync();
    }

    private static GatewayRefundCompletedIntegrationEvent RefundEvent(
        Guid orgId, Guid paymentRecordId, string gatewayTxId, decimal amount, decimal tax = 0m, decimal fee = 0m) =>
        new(
            OrganizationId: orgId,
            SubscriptionId: Guid.CreateVersion7(),
            PaymentRecordId: paymentRecordId,
            GatewayTransactionId: gatewayTxId,
            RefundedAmount: amount,
            Currency: "MYR",
            RefundedFee: fee,
            NetRefundedAmount: amount - fee,
            TaxAmount: tax);

    [Test]
    public async Task FullRefund_WithTax_ReversesFullTaxLiability()
    {
        const string tx = "txn_full";
        await SeedOriginalPaymentAsync(tx, gross: 100m, tax: 8m);
        var paymentRecordId = Guid.CreateVersion7();

        await _handler.HandleAsync(RefundEvent(_orgId, paymentRecordId, tx, amount: 108m, tax: 0m));

        var refund = await _db.LedgerEntries.Include(e => e.Lines)
            .SingleAsync(e => e.ReferenceType == LedgerReferenceTypes.GatewayRefund);

        var taxLine = refund.Lines.Single(l => l.AccountType == AccountTypes.LiabilityTaxPayable);
        Assert.That(taxLine.Amount, Is.EqualTo(8m));
        var contra = refund.Lines.Single(l => l.AccountType == AccountTypes.ContraRevenueRefunds);
        Assert.That(contra.Amount, Is.EqualTo(100m));
    }

    [Test]
    public async Task PartialRefund_50Percent_ReversesHalfTax()
    {
        const string tx = "txn_partial";
        await SeedOriginalPaymentAsync(tx, gross: 100m, tax: 8m);
        var paymentRecordId = Guid.CreateVersion7();

        // 50% of 108 = 54 → tax = 4
        await _handler.HandleAsync(RefundEvent(_orgId, paymentRecordId, tx, amount: 54m, tax: 0m));

        var refund = await _db.LedgerEntries.Include(e => e.Lines)
            .SingleAsync(e => e.ReferenceType == LedgerReferenceTypes.GatewayRefund);

        var taxLine = refund.Lines.Single(l => l.AccountType == AccountTypes.LiabilityTaxPayable);
        Assert.That(taxLine.Amount, Is.EqualTo(4m));
    }

    [Test]
    public async Task MissingOriginalPayment_TaxIsZero()
    {
        var paymentRecordId = Guid.CreateVersion7();

        await _handler.HandleAsync(RefundEvent(_orgId, paymentRecordId, "txn_missing", amount: 50m, tax: 0m));

        var refund = await _db.LedgerEntries.Include(e => e.Lines)
            .SingleAsync(e => e.ReferenceType == LedgerReferenceTypes.GatewayRefund);

        Assert.That(refund.Lines.Any(l => l.AccountType == AccountTypes.LiabilityTaxPayable), Is.False);
        var contra = refund.Lines.Single(l => l.AccountType == AccountTypes.ContraRevenueRefunds);
        Assert.That(contra.Amount, Is.EqualTo(50m));
    }

    [Test]
    public async Task ExplicitTaxOnEvent_PreferredOverProportional()
    {
        const string tx = "txn_explicit";
        await SeedOriginalPaymentAsync(tx, gross: 100m, tax: 8m);
        var paymentRecordId = Guid.CreateVersion7();

        await _handler.HandleAsync(RefundEvent(_orgId, paymentRecordId, tx, amount: 54m, tax: 2m));

        var refund = await _db.LedgerEntries.Include(e => e.Lines)
            .SingleAsync(e => e.ReferenceType == LedgerReferenceTypes.GatewayRefund);

        var taxLine = refund.Lines.Single(l => l.AccountType == AccountTypes.LiabilityTaxPayable);
        Assert.That(taxLine.Amount, Is.EqualTo(2m));
    }

    [Test]
    public async Task SecondEvent_IsIdempotent()
    {
        const string tx = "txn_idem";
        await SeedOriginalPaymentAsync(tx, gross: 100m, tax: 8m);
        var paymentRecordId = Guid.CreateVersion7();
        var evt = RefundEvent(_orgId, paymentRecordId, tx, amount: 108m);

        await _handler.HandleAsync(evt);
        await _handler.HandleAsync(evt);

        Assert.That(
            await _db.LedgerEntries.CountAsync(e => e.ReferenceType == LedgerReferenceTypes.GatewayRefund),
            Is.EqualTo(1));
    }
}
