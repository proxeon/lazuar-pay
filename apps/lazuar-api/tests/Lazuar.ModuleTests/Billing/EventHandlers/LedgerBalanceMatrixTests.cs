using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Billing.Application;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Infrastructure;
using Modules.Billing.Infrastructure.EventHandlers;
using Modules.Billing.Infrastructure.Repositories;
using Modules.Billing.Infrastructure.Services;
using Modules.Payments.Contracts;
using Modules.Payments.Contracts.Events;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Billing.EventHandlers;

/// <summary>
/// C.9 — Ledger balance matrix for payment / refund / top-up paths.
/// Asserts double-entry balance and net-revenue math operators can trust for ops dashboards.
/// </summary>
[TestFixture]
public class LedgerBalanceMatrixTests
{
    private BillingDbContext _db = null!;
    private ILedgerRepository _repo = null!;
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
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private static void AssertEntryBalanced(LedgerEntry entry)
    {
        var net = entry.Lines.Sum(l => l.BaseCurrencyAmount);
        Assert.That(net, Is.EqualTo(0m), $"Entry {entry.ReferenceType}/{entry.ReferenceId} unbalanced: {net}");
    }

    /// <summary>
    /// Mirrors <see cref="BillingQueryService.GetFinancialSummaryAsync"/> signed-sum polarity
    /// so ops net revenue stays believable without requiring live Postgres in ModuleTests.
    /// </summary>
    private static (decimal gross, decimal fees, decimal tax, decimal contra, decimal net) ComputeSummary(
        IEnumerable<LedgerEntry> entries)
    {
        var lines = entries.SelectMany(e => e.Lines).ToList();
        var gross = -lines.Where(l => l.AccountType == AccountTypes.RevenueGross).Sum(l => l.BaseCurrencyAmount);
        var fees = lines.Where(l => l.AccountType == AccountTypes.ExpenseGatewayFee).Sum(l => l.BaseCurrencyAmount);
        var tax = -lines.Where(l => l.AccountType == AccountTypes.LiabilityTaxPayable).Sum(l => l.BaseCurrencyAmount);
        var contra = lines.Where(l => l.AccountType == AccountTypes.ContraRevenueRefunds).Sum(l => l.BaseCurrencyAmount);
        var net = gross - contra - fees - tax;
        return (gross, fees, tax, contra, net);
    }

    [Test]
    public async Task Payment_PostsBalancedSale_AndIsIdempotent()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GenerateNextSequenceNumberCommand>(), Arg.Any<CancellationToken>())
            .Returns("RCPT-MATRIX-1");
        mediator.Send(Arg.Any<GenerateAndStoreDocumentCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var paymentHandler = new GatewayPaymentCompletedHandler(_repo, mediator);

        var evt = new GatewayPaymentCompletedIntegrationEvent(
            OrganizationId: _orgId,
            GatewayTransactionId: "pi_matrix_1",
            AmountPaid: 108m,
            Currency: "MYR",
            GatewayFee: 3m,
            TaxAmount: 8m,
            NetAmount: 105m,
            FxRate: 1m,
            BaseCurrency: "MYR",
            LineItems: new List<LineItemDto>(),
            Metadata: new Dictionary<string, string> { ["type"] = "commerce_subscription", ["is_b2b_required"] = "false" });

        await paymentHandler.HandleAsync(evt);
        await paymentHandler.HandleAsync(evt); // idempotent

        var entries = await _db.LedgerEntries.IgnoreQueryFilters().Include(e => e.Lines).ToListAsync();
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].ReferenceType, Is.EqualTo(LedgerReferenceTypes.GatewayPayment));
        AssertEntryBalanced(entries[0]);
        Assert.That(entries[0].ConsolidationStatus, Is.EqualTo(ConsolidationStatuses.Pending));

        var summary = ComputeSummary(entries);
        Assert.That(summary.gross, Is.EqualTo(100m)); // 108 - 8 tax
        Assert.That(summary.fees, Is.EqualTo(3m));
        Assert.That(summary.tax, Is.EqualTo(8m));
        Assert.That(summary.net, Is.EqualTo(89m)); // 100 - 3 - 8
    }

    [Test]
    public async Task PaymentThenFullRefund_NetsRevenueToZeroGrossMinusFees()
    {
        // Seed original payment lines directly so refund proportional tax resolution works.
        var sale = new LedgerEntry(_orgId, LedgerReferenceTypes.GatewayPayment, "pi_refund_1", "sale", "B2C");
        sale.AddLine(AccountTypes.AssetCash, 105m, "MYR", 105m, "MYR");
        sale.AddLine(AccountTypes.ExpenseGatewayFee, 3m, "MYR", 3m, "MYR");
        sale.AddLine(AccountTypes.RevenueGross, -100m, "MYR", -100m, "MYR");
        sale.AddLine(AccountTypes.LiabilityTaxPayable, -8m, "MYR", -8m, "MYR");
        sale.ValidateBalanced();
        sale.AssignB2cReceipt("RCPT-R1");
        _db.LedgerEntries.Add(sale);
        await _db.SaveChangesAsync();

        var refundHandler = new GatewayRefundCompletedHandler(_repo, _db);
        var paymentRecordId = Guid.CreateVersion7();
        await refundHandler.HandleAsync(new GatewayRefundCompletedIntegrationEvent(
            OrganizationId: _orgId,
            SubscriptionId: Guid.CreateVersion7(),
            PaymentRecordId: paymentRecordId,
            GatewayTransactionId: "pi_refund_1",
            RefundedAmount: 108m,
            Currency: "MYR",
            RefundedFee: 0m,
            NetRefundedAmount: 108m,
            TaxAmount: 0m));

        // Second delivery must not double-post
        await refundHandler.HandleAsync(new GatewayRefundCompletedIntegrationEvent(
            OrganizationId: _orgId,
            SubscriptionId: Guid.CreateVersion7(),
            PaymentRecordId: paymentRecordId,
            GatewayTransactionId: "pi_refund_1",
            RefundedAmount: 108m,
            Currency: "MYR",
            RefundedFee: 0m,
            NetRefundedAmount: 108m,
            TaxAmount: 0m));

        var entries = await _db.LedgerEntries.IgnoreQueryFilters().Include(e => e.Lines).ToListAsync();
        Assert.That(entries, Has.Count.EqualTo(2));
        foreach (var e in entries)
            AssertEntryBalanced(e);

        var summary = ComputeSummary(entries);
        // Gross 100, full contra 100 → gross-contra = 0; tax reversed to 0; fee remains 3 → net -3
        Assert.That(summary.gross, Is.EqualTo(100m));
        Assert.That(summary.contra, Is.EqualTo(100m));
        Assert.That(summary.tax, Is.EqualTo(0m));
        Assert.That(summary.fees, Is.EqualTo(3m));
        Assert.That(summary.net, Is.EqualTo(-3m));
    }

    [Test]
    public async Task TopUp_PostsBalancedExpense_AndDoesNotAffectMerchantNetRevenue()
    {
        var creditOptions = Options.Create(new CreditCostOptions
        {
            Packages =
            [
                new CreditPackageOption { AmountMyr = 50m, Credits = 600 }
            ]
        });
        var topUpHandler = new PlatformTopUpEventHandler(_db, creditOptions);

        await topUpHandler.HandleAsync(new GatewayPaymentCompletedIntegrationEvent(
            OrganizationId: _orgId,
            GatewayTransactionId: "pi_topup_1",
            AmountPaid: 50m,
            Currency: "MYR",
            GatewayFee: 0m,
            TaxAmount: 0m,
            NetAmount: 50m,
            FxRate: 1m,
            BaseCurrency: "MYR",
            LineItems: new List<LineItemDto>(),
            Metadata: new Dictionary<string, string>
            {
                ["type"] = "utility_credit_topup",
                ["tenant_id"] = _orgId.ToString()
            }));

        // Also run commerce payment handler skip path: utility top-up must not dual-post as GMV.
        var mediator = Substitute.For<IMediator>();
        var paymentHandler = new GatewayPaymentCompletedHandler(_repo, mediator);
        await paymentHandler.HandleAsync(new GatewayPaymentCompletedIntegrationEvent(
            OrganizationId: _orgId,
            GatewayTransactionId: "pi_topup_1",
            AmountPaid: 50m,
            Currency: "MYR",
            GatewayFee: 0m,
            TaxAmount: 0m,
            NetAmount: 50m,
            FxRate: 1m,
            BaseCurrency: "MYR",
            LineItems: new List<LineItemDto>(),
            Metadata: new Dictionary<string, string>
            {
                ["type"] = "utility_credit_topup",
                ["tenant_id"] = _orgId.ToString()
            }));

        var entries = await _db.LedgerEntries.IgnoreQueryFilters().Include(e => e.Lines).ToListAsync();
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].ReferenceType, Is.EqualTo(LedgerReferenceTypes.SystemCreditTopup));
        AssertEntryBalanced(entries[0]);

        var wallet = await _db.TenantCreditBalances.IgnoreQueryFilters()
            .SingleAsync(w => w.OrganizationId == _orgId);
        Assert.That(wallet.AvailableCredits, Is.EqualTo(600));

        var summary = ComputeSummary(entries);
        // Top-up is EXPENSE_SOFTWARE / ASSET_CASH — must not move merchant gross/net.
        Assert.That(summary.gross, Is.EqualTo(0m));
        Assert.That(summary.net, Is.EqualTo(0m));
    }

    [Test]
    public async Task Matrix_PaymentRefundTopUp_AllBalanced_IndependentPaths()
    {
        // Payment
        var sale = new LedgerEntry(_orgId, LedgerReferenceTypes.GatewayPayment, "pi_m", "sale", "B2C");
        sale.AddLine(AccountTypes.AssetCash, 97m, "MYR", 97m, "MYR");
        sale.AddLine(AccountTypes.ExpenseGatewayFee, 3m, "MYR", 3m, "MYR");
        sale.AddLine(AccountTypes.RevenueGross, -100m, "MYR", -100m, "MYR");
        sale.ValidateBalanced();
        sale.AssignB2cReceipt("RCPT-M");
        _db.LedgerEntries.Add(sale);

        // Partial refund 50% of 100 gross
        var refund = new LedgerEntry(_orgId, LedgerReferenceTypes.GatewayRefund, Guid.CreateVersion7().ToString(), "refund");
        refund.AddLine(AccountTypes.AssetCash, -50m, "MYR", -50m, "MYR");
        refund.AddLine(AccountTypes.ContraRevenueRefunds, 50m, "MYR", 50m, "MYR");
        refund.ValidateBalanced();
        _db.LedgerEntries.Add(refund);

        // Top-up
        var topUp = new LedgerEntry(_orgId, LedgerReferenceTypes.SystemCreditTopup, "pi_top", "topup", "B2B");
        topUp.AddLine(AccountTypes.ExpenseSoftwareSubscription, 50m, "MYR", 50m, "MYR");
        topUp.AddLine(AccountTypes.AssetCash, -50m, "MYR", -50m, "MYR");
        topUp.ValidateBalanced();
        topUp.MarkConsolidationNotRequired();
        _db.LedgerEntries.Add(topUp);

        await _db.SaveChangesAsync();

        var entries = await _db.LedgerEntries.IgnoreQueryFilters().Include(e => e.Lines).ToListAsync();
        Assert.That(entries, Has.Count.EqualTo(3));
        foreach (var e in entries)
            AssertEntryBalanced(e);

        var summary = ComputeSummary(entries);
        Assert.That(summary.gross, Is.EqualTo(100m));
        Assert.That(summary.contra, Is.EqualTo(50m));
        Assert.That(summary.fees, Is.EqualTo(3m));
        // net = 100 - 50 - 3 - 0 tax = 47
        Assert.That(summary.net, Is.EqualTo(47m));
    }

    [Test]
    public async Task PlatformSaasFee_DoesNotPostGatewayPaymentOrGrossRevenue()
    {
        var mediator = Substitute.For<IMediator>();
        var paymentHandler = new GatewayPaymentCompletedHandler(_repo, mediator);
        var evt = new GatewayPaymentCompletedIntegrationEvent(
            OrganizationId: PlatformCheckoutTypes.SystemOrganizationId,
            GatewayTransactionId: "pi_saas_gmv",
            AmountPaid: 99m,
            Currency: "MYR",
            GatewayFee: 0m,
            TaxAmount: 0m,
            NetAmount: 99m,
            FxRate: 1m,
            BaseCurrency: "MYR",
            LineItems: new List<LineItemDto>(),
            Metadata: new Dictionary<string, string>
            {
                ["type"] = PlatformCheckoutTypes.PlatformSaasFee,
                ["tenant_id"] = _orgId.ToString()
            });

        await paymentHandler.HandleAsync(evt);

        Assert.That(await _db.LedgerEntries.IgnoreQueryFilters().CountAsync(), Is.EqualTo(0));
        await mediator.DidNotReceive().Send(Arg.Any<GenerateAndStoreDocumentCommand>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CommerceSaasSubscriptionMetadata_StillTakesGmvPath()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GenerateNextSequenceNumberCommand>(), Arg.Any<CancellationToken>())
            .Returns("RCPT-SAAS-META");
        mediator.Send(Arg.Any<GenerateAndStoreDocumentCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var paymentHandler = new GatewayPaymentCompletedHandler(_repo, mediator);

        await paymentHandler.HandleAsync(new GatewayPaymentCompletedIntegrationEvent(
            OrganizationId: _orgId,
            GatewayTransactionId: "pi_commerce_saas",
            AmountPaid: 50m,
            Currency: "MYR",
            GatewayFee: 0m,
            TaxAmount: 0m,
            NetAmount: 50m,
            FxRate: 1m,
            BaseCurrency: "MYR",
            LineItems: new List<LineItemDto>(),
            Metadata: new Dictionary<string, string>
            {
                ["type"] = "saas_subscription",
                ["is_b2b_required"] = "false"
            }));

        var entries = await _db.LedgerEntries.IgnoreQueryFilters().Include(e => e.Lines).ToListAsync();
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].ReferenceType, Is.EqualTo(LedgerReferenceTypes.GatewayPayment));
        Assert.That(entries[0].Lines.Any(l => l.AccountType == AccountTypes.RevenueGross), Is.True);
    }

    [Test]
    public async Task GuestGmvPayment_StillZeroPlatformTake_DoesNotCreateSaasFeeOrCredits()
    {
        var mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<GenerateNextSequenceNumberCommand>(), Arg.Any<CancellationToken>())
            .Returns("RCPT-GMV-0");
        mediator.Send(Arg.Any<GenerateAndStoreDocumentCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var paymentHandler = new GatewayPaymentCompletedHandler(_repo, mediator);
        var topUpHandler = new PlatformTopUpEventHandler(_db, Options.Create(new CreditCostOptions
        {
            Packages = [new CreditPackageOption { AmountMyr = 50m, Credits = 600 }]
        }));
        var saasHandler = new PlatformSaasFeeHandler(
            _db,
            mediator,
            Substitute.For<IEventBus>(),
            Options.Create(new SaasOptions
            {
                Plan = new SaasPlanOptions
                {
                    Code = "hub_starter",
                    Name = "Hub Starter",
                    AmountMyr = 50m,
                    Interval = "mo",
                    Currency = "MYR"
                }
            }),
            NullLogger<PlatformSaasFeeHandler>.Instance);

        var evt = new GatewayPaymentCompletedIntegrationEvent(
            OrganizationId: _orgId,
            GatewayTransactionId: "pi_guest_gmv",
            AmountPaid: 50m,
            Currency: "MYR",
            GatewayFee: 0m,
            TaxAmount: 0m,
            NetAmount: 50m,
            FxRate: 1m,
            BaseCurrency: "MYR",
            LineItems: new List<LineItemDto>(),
            Metadata: new Dictionary<string, string>
            {
                ["type"] = "commerce_subscription",
                ["tenant_id"] = _orgId.ToString(),
                ["is_b2b_required"] = "false"
            });

        await paymentHandler.HandleAsync(evt);
        await topUpHandler.HandleAsync(evt);
        await saasHandler.HandleAsync(evt);

        var entries = await _db.LedgerEntries.IgnoreQueryFilters().Include(e => e.Lines).ToListAsync();
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].ReferenceType, Is.EqualTo(LedgerReferenceTypes.GatewayPayment));
        Assert.That(entries[0].Lines.Any(l => l.AccountType == AccountTypes.RevenueGross), Is.True);
        Assert.That(
            entries.Any(e => e.ReferenceType == LedgerReferenceTypes.SystemSaasFee),
            Is.False);
        Assert.That(await _db.TenantCreditBalances.IgnoreQueryFilters().CountAsync(), Is.EqualTo(0));
        Assert.That(await _db.WorkspaceSaasSubscriptions.IgnoreQueryFilters().CountAsync(), Is.EqualTo(0));

        var summary = ComputeSummary(entries);
        Assert.That(summary.gross, Is.EqualTo(50m));
        Assert.That(summary.fees, Is.EqualTo(0m));
    }
}
