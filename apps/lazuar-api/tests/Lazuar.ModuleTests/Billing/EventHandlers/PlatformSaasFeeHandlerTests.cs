using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Lazuar.TestSupport;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Modules.Billing.Application.Queries;
using Modules.Billing.Contracts.Commands;
using Modules.Billing.Contracts.Events;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Infrastructure;
using Modules.Billing.Infrastructure.EventHandlers;
using Modules.Billing.Infrastructure.Queries;
using Modules.Billing.Infrastructure.Services;
using Modules.Payments.Contracts;
using Modules.Payments.Contracts.Events;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Billing.EventHandlers;

[TestFixture]
public class PlatformSaasFeeHandlerTests
{
    private BillingDbContext _db = null!;
    private IMediator _mediator = null!;
    private IEventBus _eventBus = null!;
    private PlatformSaasFeeHandler _handler = null!;
    private Guid _tenantId;
    private const decimal PlanAmount = 99m;

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
        _eventBus = Substitute.For<IEventBus>();
        _mediator.Send(Arg.Any<GenerateNextSequenceNumberCommand>(), Arg.Any<CancellationToken>())
            .Returns("SAAS-2026-00001");
        _mediator.Send(Arg.Any<GenerateAndStorePlatformSaasInvoiceCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _handler = new PlatformSaasFeeHandler(
            _db,
            _mediator,
            _eventBus,
            Options.Create(FixtureSaas()),
            NullLogger<PlatformSaasFeeHandler>.Instance);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    private static SaasOptions FixtureSaas() => new()
    {
        Plan = new SaasPlanOptions
        {
            Code = "hub_starter",
            Name = "Hub Starter",
            AmountMyr = PlanAmount,
            Interval = "mo",
            Currency = "MYR"
        },
        Seller = new SaasSellerOptions
        {
            LegalName = "Lazuar",
            SstRate = 0,
            SstReason = "Supplier not SST-registered"
        }
    };

    private GatewayPaymentCompletedIntegrationEvent PaidEvent(
        Guid tenantId,
        string txId,
        decimal amount = PlanAmount,
        string type = PlatformCheckoutTypes.PlatformSaasFee,
        string? tenantMeta = "set",
        Guid? organizationId = null) =>
        new(
            OrganizationId: organizationId ?? PlatformCheckoutTypes.SystemOrganizationId,
            GatewayTransactionId: txId,
            AmountPaid: amount,
            Currency: "MYR",
            GatewayFee: 0,
            TaxAmount: 0,
            NetAmount: amount,
            FxRate: 1m,
            BaseCurrency: "MYR",
            LineItems: new List<LineItemDto>(),
            Metadata: BuildMeta(type, tenantId, tenantMeta));

    private static Dictionary<string, string> BuildMeta(string type, Guid tenantId, string? tenantMeta)
    {
        var meta = new Dictionary<string, string> { ["type"] = type };
        if (tenantMeta == "set")
            meta["tenant_id"] = tenantId.ToString();
        else if (tenantMeta == "system")
            meta["tenant_id"] = PlatformCheckoutTypes.SystemOrganizationId.ToString();
        return meta;
    }

    [Test]
    public async Task HandleAsync_HappyPath_Activates_BooksExpense_GrantsNoCredits()
    {
        await _handler.HandleAsync(PaidEvent(_tenantId, "saas_tx_1"));

        var sub = await _db.WorkspaceSaasSubscriptions.IgnoreQueryFilters()
            .SingleAsync(s => s.OrganizationId == _tenantId);
        Assert.That(sub.Status, Is.EqualTo(WorkspaceSaasStatuses.Active));
        Assert.That(sub.CurrentPeriodEnd, Is.Not.Null);
        Assert.That(sub.CurrentPeriodEnd!.Value, Is.GreaterThan(DateTime.UtcNow.AddDays(20)));

        var entry = await _db.LedgerEntries.IgnoreQueryFilters().Include(e => e.Lines)
            .SingleAsync(e => e.ReferenceType == LedgerReferenceTypes.SystemSaasFee);
        Assert.That(entry.OrganizationId, Is.EqualTo(_tenantId));
        Assert.That(entry.ReferenceId, Is.EqualTo("saas_tx_1"));
        Assert.That(entry.Lines.Sum(l => l.BaseCurrencyAmount), Is.EqualTo(0m));
        Assert.That(
            entry.Lines.Single(l => l.AccountType == AccountTypes.ExpenseSoftwareSubscription).Amount,
            Is.EqualTo(PlanAmount));
        Assert.That(
            entry.Lines.Single(l => l.AccountType == AccountTypes.AssetCash).Amount,
            Is.EqualTo(-PlanAmount));
        Assert.That(entry.CustomerDocumentNumber, Is.EqualTo("SAAS-2026-00001"));

        Assert.That(await _db.TenantCreditBalances.IgnoreQueryFilters().CountAsync(), Is.EqualTo(0));

        await _mediator.Received(1).Send(
            Arg.Is<GenerateNextSequenceNumberCommand>(c =>
                c.OrganizationId == PlatformCheckoutTypes.SystemOrganizationId
                && c.Prefix.StartsWith("SAAS-", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
        await _mediator.Received(1).Send(
            Arg.Is<GenerateAndStorePlatformSaasInvoiceCommand>(c =>
                c.PayingOrganizationId == _tenantId && c.LedgerEntryId == entry.Id),
            Arg.Any<CancellationToken>());
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<InvoiceIssuedIntegrationEvent>());

        var topUp = new PlatformTopUpEventHandler(_db, Options.Create(new CreditCostOptions
        {
            Packages = [new CreditPackageOption { AmountMyr = PlanAmount, Credits = 1100 }]
        }));
        await topUp.HandleAsync(PaidEvent(_tenantId, "saas_tx_1"));
        Assert.That(await _db.TenantCreditBalances.IgnoreQueryFilters().CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task HandleAsync_IdempotentRedelivery_DoesNotDoublePeriod()
    {
        await _handler.HandleAsync(PaidEvent(_tenantId, "saas_tx_idem"));
        var firstEnd = (await _db.WorkspaceSaasSubscriptions.IgnoreQueryFilters()
            .SingleAsync(s => s.OrganizationId == _tenantId)).CurrentPeriodEnd;

        await _handler.HandleAsync(PaidEvent(_tenantId, "saas_tx_idem"));

        Assert.That(await _db.LedgerEntries.IgnoreQueryFilters().CountAsync(), Is.EqualTo(1));
        var again = await _db.WorkspaceSaasSubscriptions.IgnoreQueryFilters()
            .SingleAsync(s => s.OrganizationId == _tenantId);
        Assert.That(again.CurrentPeriodEnd, Is.EqualTo(firstEnd));
    }

    [Test]
    public async Task HandleAsync_WrongType_IsNoOp()
    {
        await _handler.HandleAsync(PaidEvent(_tenantId, "tx_commerce", type: "commerce_subscription"));
        await _handler.HandleAsync(PaidEvent(_tenantId, "tx_topup", type: PlatformCheckoutTypes.UtilityCreditTopup));
        await _handler.HandleAsync(PaidEvent(_tenantId, "tx_saas_meta", type: "saas_subscription"));

        Assert.That(await _db.LedgerEntries.IgnoreQueryFilters().CountAsync(), Is.EqualTo(0));
        Assert.That(await _db.WorkspaceSaasSubscriptions.IgnoreQueryFilters().CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task HandleAsync_MissingTenantId_DoesNotBookOnSystemOrg()
    {
        await _handler.HandleAsync(PaidEvent(_tenantId, "tx_no_tenant", tenantMeta: null));
        await _handler.HandleAsync(PaidEvent(_tenantId, "tx_system_tenant", tenantMeta: "system"));

        Assert.That(await _db.LedgerEntries.IgnoreQueryFilters().CountAsync(), Is.EqualTo(0));
        Assert.That(
            await _db.WorkspaceSaasSubscriptions.IgnoreQueryFilters()
                .CountAsync(s => s.OrganizationId == PlatformCheckoutTypes.SystemOrganizationId),
            Is.EqualTo(0));
    }

    [Test]
    public async Task HandleAsync_AmountMismatch_DoesNotActivate()
    {
        await _handler.HandleAsync(PaidEvent(_tenantId, "tx_wrong_amt", amount: 50m));

        Assert.That(await _db.WorkspaceSaasSubscriptions.IgnoreQueryFilters().CountAsync(), Is.EqualTo(0));
        Assert.That(await _db.LedgerEntries.IgnoreQueryFilters().CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task HandleAsync_EmptyGatewayTx_IsNoOp()
    {
        await _handler.HandleAsync(PaidEvent(_tenantId, ""));
        Assert.That(await _db.LedgerEntries.IgnoreQueryFilters().CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task GetWorkspaceSaas_UnpaidThenActive()
    {
        var saas = Options.Create(FixtureSaas());
        var get = new GetWorkspaceSaasQueryHandler(_db, saas);

        var before = await get.Handle(new GetWorkspaceSaasQuery(_tenantId), CancellationToken.None);
        Assert.That(before.Status, Is.EqualTo(WorkspaceSaasStatuses.Unpaid));
        Assert.That(before.Plan.AmountMyr, Is.EqualTo(PlanAmount));

        await _handler.HandleAsync(PaidEvent(_tenantId, "saas_tx_get"));

        var after = await get.Handle(new GetWorkspaceSaasQuery(_tenantId), CancellationToken.None);
        Assert.That(after.Status, Is.EqualTo(WorkspaceSaasStatuses.Active));
        Assert.That(after.CurrentPeriodEnd, Is.Not.Null);
    }

    [Test]
    public async Task HandleAsync_SecondPayment_ExtendsFromCurrentEnd()
    {
        await _handler.HandleAsync(PaidEvent(_tenantId, "saas_tx_a"));
        var first = await _db.WorkspaceSaasSubscriptions.IgnoreQueryFilters()
            .SingleAsync(s => s.OrganizationId == _tenantId);
        var firstEnd = first.CurrentPeriodEnd!.Value;

        await _handler.HandleAsync(PaidEvent(_tenantId, "saas_tx_b"));

        var second = await _db.WorkspaceSaasSubscriptions.IgnoreQueryFilters()
            .SingleAsync(s => s.OrganizationId == _tenantId);
        Assert.That(second.CurrentPeriodStart, Is.EqualTo(firstEnd));
        Assert.That(second.CurrentPeriodEnd, Is.EqualTo(firstEnd.AddMonths(1)));
        Assert.That(
            await _db.LedgerEntries.IgnoreQueryFilters()
                .CountAsync(e => e.ReferenceType == LedgerReferenceTypes.SystemSaasFee),
            Is.EqualTo(2));
    }
}
