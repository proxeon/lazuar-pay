using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BuildingBlocks.Infrastructure;
using Lazuar.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Infrastructure;
using Modules.Billing.Infrastructure.EventHandlers;
using Modules.Billing.Infrastructure.Services;
using Modules.Payments.Contracts;
using Modules.Payments.Contracts.Events;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Billing.EventHandlers;

[TestFixture]
public class PlatformTopUpEventHandlerTests
{
    private BillingDbContext _dbContext = null!;
    private PlatformTopUpEventHandler _handler = null!;
    private Guid _tenantId;

    [SetUp]
    public void SetUp()
    {
        _tenantId = Guid.CreateVersion7();

        _dbContext = new BillingDbContext(
            InMemoryDb.CreateOptions<BillingDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());

        var creditOptions = Options.Create(new CreditCostOptions
        {
            Packages =
            [
                new CreditPackageOption { AmountMyr = 10m, Credits = 100 },
                new CreditPackageOption { AmountMyr = 50m, Credits = 600 }
            ]
        });

        _handler = new PlatformTopUpEventHandler(_dbContext, creditOptions);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Dispose();
    }

    private static GatewayPaymentCompletedIntegrationEvent CreateEvent(
        Guid tenantId,
        string gatewayTransactionId,
        decimal amountPaid = 50m)
    {
        return new GatewayPaymentCompletedIntegrationEvent(
            OrganizationId: tenantId,
            GatewayTransactionId: gatewayTransactionId,
            AmountPaid: amountPaid,
            Currency: "MYR",
            GatewayFee: 0,
            TaxAmount: 0,
            NetAmount: amountPaid,
            FxRate: 1m,
            BaseCurrency: "MYR",
            LineItems: new List<LineItemDto>(),
            Metadata: new Dictionary<string, string>
            {
                ["type"] = "utility_credit_topup",
                ["tenant_id"] = tenantId.ToString()
            });
    }

    [Test]
    public async Task HandleAsync_Skips_When_GatewayTransactionId_Empty()
    {
        await _handler.HandleAsync(CreateEvent(_tenantId, gatewayTransactionId: ""));

        Assert.That(await _dbContext.LedgerEntries.IgnoreQueryFilters().CountAsync(), Is.EqualTo(0));
        Assert.That(await _dbContext.TenantCreditBalances.IgnoreQueryFilters().CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task HandleAsync_Skips_When_SYSTEM_CREDIT_TOPUP_Already_Processed()
    {
        var existing = new LedgerEntry(
            _tenantId,
            "SYSTEM_CREDIT_TOPUP",
            "txn_already",
            "prior top-up",
            "B2B");
        existing.AddLine("EXPENSE_SOFTWARE_SUBSCRIPTION", 50m, "MYR", 50m, "MYR");
        existing.AddLine("ASSET_CASH", -50m, "MYR", -50m, "MYR");
        existing.ValidateBalanced();
        _dbContext.LedgerEntries.Add(existing);
        await _dbContext.SaveChangesAsync();

        await _handler.HandleAsync(CreateEvent(_tenantId, "txn_already"));

        Assert.That(await _dbContext.LedgerEntries.IgnoreQueryFilters().CountAsync(), Is.EqualTo(1));
        Assert.That(await _dbContext.TenantCreditBalances.IgnoreQueryFilters().CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task HandleAsync_TopUps_And_Records_Ledger_Once()
    {
        await _handler.HandleAsync(CreateEvent(_tenantId, "txn_new", amountPaid: 50m));

        var wallet = await _dbContext.TenantCreditBalances
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.OrganizationId == _tenantId);
        Assert.That(wallet, Is.Not.Null);
        Assert.That(wallet!.AvailableCredits, Is.EqualTo(600));

        var ledger = await _dbContext.LedgerEntries
            .IgnoreQueryFilters()
            .SingleAsync(e => e.ReferenceType == "SYSTEM_CREDIT_TOPUP" && e.ReferenceId == "txn_new");
        Assert.That(ledger.OrganizationId, Is.EqualTo(_tenantId));

        // Second delivery of the same payment must not double-credit
        await _handler.HandleAsync(CreateEvent(_tenantId, "txn_new", amountPaid: 50m));
        wallet = await _dbContext.TenantCreditBalances
            .IgnoreQueryFilters()
            .FirstAsync(w => w.OrganizationId == _tenantId);
        Assert.That(wallet.AvailableCredits, Is.EqualTo(600));
        Assert.That(await _dbContext.LedgerEntries.IgnoreQueryFilters().CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task HandleAsync_Ignores_Non_Utility_TopUp()
    {
        var @event = new GatewayPaymentCompletedIntegrationEvent(
            OrganizationId: _tenantId,
            GatewayTransactionId: "txn_commerce",
            AmountPaid: 50m,
            Currency: "MYR",
            GatewayFee: 0,
            TaxAmount: 0,
            NetAmount: 50m,
            FxRate: 1m,
            BaseCurrency: "MYR",
            LineItems: new List<LineItemDto>(),
            Metadata: new Dictionary<string, string>
            {
                ["type"] = "commerce_subscription",
                ["tenant_id"] = _tenantId.ToString()
            });

        await _handler.HandleAsync(@event);

        Assert.That(await _dbContext.LedgerEntries.IgnoreQueryFilters().CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task HandleAsync_PlatformSaasFee_DoesNotGrantCredits()
    {
        var wallet = new TenantCreditBalance(_tenantId);
        wallet.TopUp(50, "starter");
        _dbContext.TenantCreditBalances.Add(wallet);
        await _dbContext.SaveChangesAsync();

        var @event = new GatewayPaymentCompletedIntegrationEvent(
            OrganizationId: PlatformCheckoutTypes.SystemOrganizationId,
            GatewayTransactionId: "txn_saas_no_credits",
            AmountPaid: 50m,
            Currency: "MYR",
            GatewayFee: 0,
            TaxAmount: 0,
            NetAmount: 50m,
            FxRate: 1m,
            BaseCurrency: "MYR",
            LineItems: new List<LineItemDto>(),
            Metadata: new Dictionary<string, string>
            {
                ["type"] = PlatformCheckoutTypes.PlatformSaasFee,
                ["tenant_id"] = _tenantId.ToString()
            });

        await _handler.HandleAsync(@event);

        var credits = await _dbContext.TenantCreditBalances.IgnoreQueryFilters()
            .SingleAsync(w => w.OrganizationId == _tenantId);
        Assert.That(credits.AvailableCredits, Is.EqualTo(50));
        Assert.That(
            await _dbContext.LedgerEntries.IgnoreQueryFilters()
                .CountAsync(e => e.ReferenceType == "SYSTEM_CREDIT_TOPUP"),
            Is.EqualTo(0));
    }
}
