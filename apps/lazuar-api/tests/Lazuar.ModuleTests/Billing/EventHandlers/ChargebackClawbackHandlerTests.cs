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
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var ctx = Substitute.For<IExecutionContextAccessor>();
        ctx.TenantId.Returns(Guid.Empty);
        _db = new BillingDbContext(options, ctx, Substitute.For<IMediator>(), new DatabaseJobTrigger());
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

        var reverse = await _db.LedgerEntries.Include(e => e.Lines)
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
            await _db.LedgerEntries.CountAsync(e => e.ReferenceType == LedgerReferenceTypes.SystemCreditChargeback),
            Is.EqualTo(1));
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

        Assert.That(await _db.LedgerEntries.CountAsync(), Is.EqualTo(0));
        await _mediator.DidNotReceive().Send(Arg.Any<ClawbackCreditsCommand>(), Arg.Any<CancellationToken>());
    }
}
