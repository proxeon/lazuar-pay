using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Billing.Contracts.Events;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;
using Modules.Billing.Infrastructure;
using Modules.Billing.Infrastructure.Workers;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Billing.Workers;

[TestFixture]
public class B2cConsolidationJobTests
{
    private BillingDbContext _db = null!;
    private IEventBus _eventBus = null!;
    private B2cConsolidationJob _job = null!;
    private Guid _orgId;
    private ServiceProvider _sp = null!;

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
        _eventBus = Substitute.For<IEventBus>();

        var services = new ServiceCollection();
        services.AddSingleton(_db);
        services.AddKeyedSingleton<IEventBus>("BillingEventBus", _eventBus);
        _sp = services.BuildServiceProvider();

        _job = new B2cConsolidationJob(_sp.GetRequiredService<IServiceScopeFactory>(), NullLogger<B2cConsolidationJob>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        _sp.Dispose();
    }

    private static DateTime PriorMonthUtcMid()
    {
        var myt = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Singapore Standard Time" : "Asia/Kuala_Lumpur");
        var nowMyt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, myt);
        var midPriorMyt = new DateTime(nowMyt.Year, nowMyt.Month, 15, 12, 0, 0, DateTimeKind.Unspecified).AddMonths(-1);
        return TimeZoneInfo.ConvertTimeToUtc(midPriorMyt, myt);
    }

    private LedgerEntry SeedSale(string refId, string? status, string? consolidationStatus, DateTime? timestamp = null)
    {
        var entry = new LedgerEntry(_orgId, LedgerReferenceTypes.GatewayPayment, refId, "sale", "B2C");
        entry.AddLine(AccountTypes.AssetCash, 108m, "MYR", 108m, "MYR");
        entry.AddLine(AccountTypes.RevenueGross, -100m, "MYR", -100m, "MYR");
        entry.AddLine(AccountTypes.LiabilityTaxPayable, -8m, "MYR", -8m, "MYR");
        entry.ValidateBalanced();

        if (status == LhdnValidationStatuses.B2cReceipt || consolidationStatus == ConsolidationStatuses.Pending)
            entry.AssignB2cReceipt($"RCPT-{refId}");
        else if (consolidationStatus == ConsolidationStatuses.Consolidated)
            entry.MarkConsolidatedPending($"B2C-CONS-ALREADY-{_orgId:N}");
        else if (status == null && consolidationStatus == null)
        {
            // legacy null — leave as-is for backfill eligibility
        }

        // Force timestamp into prior month window via reflection if needed.
        if (timestamp.HasValue)
        {
            typeof(LedgerEntry).GetProperty(nameof(LedgerEntry.Timestamp))!
                .SetValue(entry, timestamp.Value);
        }

        _db.LedgerEntries.Add(entry);
        return entry;
    }

    [Test]
    public async Task Selects_B2cReceipt_Pending_And_Marks_Consolidated()
    {
        var ts = PriorMonthUtcMid();
        SeedSale("tx_eligible", LhdnValidationStatuses.B2cReceipt, ConsolidationStatuses.Pending, ts);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        var entry = await _db.LedgerEntries.IgnoreQueryFilters().SingleAsync(e => e.ReferenceId == "tx_eligible");
        Assert.That(entry.ConsolidationStatus, Is.EqualTo(ConsolidationStatuses.Consolidated));
        Assert.That(entry.LhdnValidationStatus, Is.EqualTo(LhdnValidationStatuses.ConsolidatedPending));
        Assert.That(entry.CustomerDocumentNumber, Does.StartWith("RCPT-"));
        Assert.That(entry.TaxInvoiceId, Does.StartWith("B2C-CONS-"));

        await _eventBus.Received(1).PublishAsync(Arg.Any<ConsolidatedInvoiceIssuedIntegrationEvent>());
    }

    [Test]
    public async Task DoesNotReselect_AlreadyConsolidated()
    {
        var ts = PriorMonthUtcMid();
        var entry = SeedSale("tx_done", null, ConsolidationStatuses.Consolidated, ts);
        await _db.SaveChangesAsync();

        var priorStatus = entry.LhdnValidationStatus;
        var priorTax = entry.TaxInvoiceId;

        await _job.RunOnceAsync(CancellationToken.None);

        var reloaded = await _db.LedgerEntries.IgnoreQueryFilters().SingleAsync(e => e.ReferenceId == "tx_done");
        Assert.That(reloaded.ConsolidationStatus, Is.EqualTo(ConsolidationStatuses.Consolidated));
        Assert.That(reloaded.TaxInvoiceId, Is.EqualTo(priorTax));
        Assert.That(reloaded.LhdnValidationStatus, Is.EqualTo(priorStatus));

        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<ConsolidatedInvoiceIssuedIntegrationEvent>());
    }

    [Test]
    public async Task SecondRun_SamePeriod_IsIdempotent()
    {
        var ts = PriorMonthUtcMid();
        SeedSale("tx_once", LhdnValidationStatuses.B2cReceipt, ConsolidationStatuses.Pending, ts);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);
        await _job.RunOnceAsync(CancellationToken.None);

        await _eventBus.Received(1).PublishAsync(Arg.Any<ConsolidatedInvoiceIssuedIntegrationEvent>());
        Assert.That(
            await _db.LedgerEntries.IgnoreQueryFilters()
                .CountAsync(e => e.ConsolidationStatus == ConsolidationStatuses.Consolidated),
            Is.EqualTo(1));
    }
}
