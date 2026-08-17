using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Infrastructure;
using Lazuar.TestSupport;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Infrastructure;
using Modules.Billing.Infrastructure.EventHandlers;
using Modules.Billing.Infrastructure.Services;
using Modules.Payments.Contracts.Events;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Billing.EventHandlers;

[TestFixture]
public class ChargebackClawbackHandlerTests
{
    private BillingDbContext _db = null!;
    private IMediator _mediator = null!;
    private ChargebackClawbackHandler _handler = null!;
    private Guid _tenantId;

    [SetUp]
    public void SetUp()
    {
        _tenantId = Guid.CreateVersion7();
        _db = new BillingDbContext(
            InMemoryDb.CreateOptions<BillingDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());
        _mediator = Substitute.For<IMediator>();

        var creditOptions = Options.Create(new CreditCostOptions
        {
            Packages =
            [
                new CreditPackageOption { AmountMyr = 50m, Credits = 600 }
            ]
        });

        _handler = new ChargebackClawbackHandler(
            _mediator,
            _db,
            creditOptions,
            NullLogger<ChargebackClawbackHandler>.Instance);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private async Task SeedTopUpAsync(string gatewayTxId, decimal amount = 50m)
    {
        var entry = new LedgerEntry(
            _tenantId,
            LedgerReferenceTypes.SystemCreditTopup,
            gatewayTxId,
            "top-up",
            "B2B");
        entry.AddLine(AccountTypes.ExpenseSoftwareSubscription, amount, "MYR", amount, "MYR");
        entry.AddLine(AccountTypes.AssetCash, -amount, "MYR", -amount, "MYR");
        entry.ValidateBalanced();
        entry.MarkConsolidationNotRequired();
        _db.LedgerEntries.Add(entry);
        await _db.SaveChangesAsync();
    }

    private GatewayDisputeCreatedIntegrationEvent Dispute(string gatewayTxId, decimal amount = 50m) =>
        new(
            OrganizationId: _tenantId,
            GatewayTransactionId: gatewayTxId,
            AmountDisputed: amount,
            Currency: "MYR",
            Metadata: new Dictionary<string, string>
            {
                ["type"] = "utility_credit_topup",
                ["tenant_id"] = _tenantId.ToString()
            });

    [Test]
    public async Task UtilityChargeback_ReversesSystemCreditTopupLedger()
    {
        const string tx = "txn_cb_1";
        await SeedTopUpAsync(tx);

        await _handler.HandleAsync(Dispute(tx));

        var reverse = await _db.LedgerEntries.IgnoreQueryFilters().Include(e => e.Lines)
            .SingleAsync(e => e.ReferenceType == LedgerReferenceTypes.SystemCreditChargeback);

        Assert.That(reverse.ReferenceId, Is.EqualTo(tx));
        Assert.That(reverse.Lines.Sum(l => l.BaseCurrencyAmount), Is.EqualTo(0m));
        Assert.That(
            reverse.Lines.Single(l => l.AccountType == AccountTypes.ExpenseSoftwareSubscription).Amount,
            Is.EqualTo(-50m));
        Assert.That(
            reverse.Lines.Single(l => l.AccountType == AccountTypes.AssetCash).Amount,
            Is.EqualTo(50m));

        await _mediator.Received(1).Send(Arg.Any<ClawbackCreditsCommand>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UtilityChargeback_IsIdempotent_OnSecondDispute()
    {
        const string tx = "txn_cb_idem";
        await SeedTopUpAsync(tx);

        await _handler.HandleAsync(Dispute(tx));
        await _handler.HandleAsync(Dispute(tx));

        Assert.That(
            await _db.LedgerEntries.IgnoreQueryFilters()
                .CountAsync(e => e.ReferenceType == LedgerReferenceTypes.SystemCreditChargeback),
            Is.EqualTo(1));
        await _mediator.Received(1).Send(Arg.Any<ClawbackCreditsCommand>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PlatformSaasFeeDispute_MarksPastDue_DoesNotClawCredits()
    {
        var sub = new WorkspaceSaasSubscription(_tenantId, "hub_starter");
        sub.ActivateFromPayment(DateTime.UtcNow, "mo", "txn_saas_cb");
        _db.WorkspaceSaasSubscriptions.Add(sub);
        var wallet = new TenantCreditBalance(_tenantId);
        wallet.TopUp(50, "starter");
        _db.TenantCreditBalances.Add(wallet);
        await _db.SaveChangesAsync();

        await _handler.HandleAsync(new GatewayDisputeCreatedIntegrationEvent(
            OrganizationId: _tenantId,
            GatewayTransactionId: "txn_saas_cb",
            AmountDisputed: 99m,
            Currency: "MYR",
            Metadata: new Dictionary<string, string>
            {
                ["type"] = "platform_saas_fee",
                ["tenant_id"] = _tenantId.ToString()
            }));

        var updated = await _db.WorkspaceSaasSubscriptions.IgnoreQueryFilters()
            .SingleAsync(s => s.OrganizationId == _tenantId);
        Assert.That(updated.Status, Is.EqualTo(WorkspaceSaasStatuses.PastDue));
        var credits = await _db.TenantCreditBalances.IgnoreQueryFilters()
            .SingleAsync(w => w.OrganizationId == _tenantId);
        Assert.That(credits.AvailableCredits, Is.EqualTo(50));
        await _mediator.DidNotReceive().Send(Arg.Any<ClawbackCreditsCommand>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task NonUtility_IsNoOp()
    {
        var @event = new GatewayDisputeCreatedIntegrationEvent(
            OrganizationId: _tenantId,
            GatewayTransactionId: "txn_commerce",
            AmountDisputed: 100m,
            Currency: "MYR",
            Metadata: new Dictionary<string, string>
            {
                ["type"] = "commerce_subscription",
                ["tenant_id"] = _tenantId.ToString()
            });

        await _handler.HandleAsync(@event);

        Assert.That(await _db.LedgerEntries.IgnoreQueryFilters().CountAsync(), Is.EqualTo(0));
        await _mediator.DidNotReceive().Send(Arg.Any<ClawbackCreditsCommand>(), Arg.Any<CancellationToken>());
    }
}
