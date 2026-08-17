using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    public async Task AlreadyConsolidated_EmptyAmbientTenant_DoesNotRepublish()
    {
        var ts = PriorMonthUtcMid();
        SeedSale("tx_new_pending", LhdnValidationStatuses.B2cReceipt, ConsolidationStatuses.Pending, ts);

        var myt = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Singapore Standard Time" : "Asia/Kuala_Lumpur");
        var periodMyt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(ts, DateTimeKind.Utc), myt);
        var periodKey = new DateTime(periodMyt.Year, periodMyt.Month, 1).ToString("yyyyMM");
        var issued = SeedSale("tx_issued_marker", LhdnValidationStatuses.B2cReceipt, ConsolidationStatuses.Pending, ts);
        issued.MarkConsolidatedPending($"B2C-CONS-{periodKey}-{_orgId:N}");
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

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

    [Test]
    public async Task CatchUp_ProcessesOlderClosedMonth_NotOnlyPriorMonth()
    {
        var ts = MonthsAgoUtcMid(2);
        SeedSale("tx_old_month", LhdnValidationStatuses.B2cReceipt, ConsolidationStatuses.Pending, ts);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        var entry = await _db.LedgerEntries.IgnoreQueryFilters().SingleAsync(e => e.ReferenceId == "tx_old_month");
        Assert.That(entry.ConsolidationStatus, Is.EqualTo(ConsolidationStatuses.Consolidated));
        Assert.That(entry.TaxInvoiceId, Does.StartWith("B2C-CONS-"));

        await _eventBus.Received(1).PublishAsync(Arg.Any<ConsolidatedInvoiceIssuedIntegrationEvent>());
    }

    private static DateTime MonthsAgoUtcMid(int monthsAgo)
    {
        var myt = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Singapore Standard Time" : "Asia/Kuala_Lumpur");
        var nowMyt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, myt);
        var mid = new DateTime(nowMyt.Year, nowMyt.Month, 15, 12, 0, 0, DateTimeKind.Unspecified).AddMonths(-monthsAgo);
        return TimeZoneInfo.ConvertTimeToUtc(mid, myt);
    }

    [Test]
    public async Task Eligibility_Includes_LegacyNullStatus_B2cReceiptAndPending()
    {
        var ts = PriorMonthUtcMid();
        SeedSale("tx_legacy_null", status: null, consolidationStatus: null, ts);
        SeedSale("tx_receipt", LhdnValidationStatuses.B2cReceipt, ConsolidationStatuses.Pending, ts);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        var legacy = await _db.LedgerEntries.IgnoreQueryFilters().SingleAsync(e => e.ReferenceId == "tx_legacy_null");
        var receipt = await _db.LedgerEntries.IgnoreQueryFilters().SingleAsync(e => e.ReferenceId == "tx_receipt");
        Assert.That(legacy.ConsolidationStatus, Is.EqualTo(ConsolidationStatuses.Consolidated));
        Assert.That(receipt.ConsolidationStatus, Is.EqualTo(ConsolidationStatuses.Consolidated));
    }

    [Test]
    public async Task Eligibility_Excludes_B2b_And_NotRequired_And_CurrentMonth()
    {
        var prior = PriorMonthUtcMid();
        var myt = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Singapore Standard Time" : "Asia/Kuala_Lumpur");
        var nowMyt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, myt);
        var currentMidMyt = new DateTime(nowMyt.Year, nowMyt.Month, 10, 12, 0, 0, DateTimeKind.Unspecified);
        var currentUtc = TimeZoneInfo.ConvertTimeToUtc(currentMidMyt, myt);

        // B2B
        var b2b = new LedgerEntry(_orgId, LedgerReferenceTypes.GatewayPayment, "tx_b2b", "b2b", "B2B");
        b2b.AddLine(AccountTypes.AssetCash, 100m, "MYR", 100m, "MYR");
        b2b.AddLine(AccountTypes.RevenueGross, -100m, "MYR", -100m, "MYR");
        b2b.ValidateBalanced();
        b2b.MarkConsolidationNotRequired();
        typeof(LedgerEntry).GetProperty(nameof(LedgerEntry.Timestamp))!.SetValue(b2b, prior);
        _db.LedgerEntries.Add(b2b);

        // B2C NOT_REQUIRED
        var notReq = SeedSale("tx_notreq", null, null, prior);
        notReq.MarkConsolidationNotRequired();

        // Current month B2C pending — closed-month catch-up must skip open month
        SeedSale("tx_current", LhdnValidationStatuses.B2cReceipt, ConsolidationStatuses.Pending, currentUtc);

        // Eligible control row
        SeedSale("tx_ok", LhdnValidationStatuses.B2cReceipt, ConsolidationStatuses.Pending, prior);
        await _db.SaveChangesAsync();

        await _job.RunOnceAsync(CancellationToken.None);

        var reloadedB2b = await _db.LedgerEntries.IgnoreQueryFilters().SingleAsync(e => e.ReferenceId == "tx_b2b");
        var reloadedNotReq = await _db.LedgerEntries.IgnoreQueryFilters().SingleAsync(e => e.ReferenceId == "tx_notreq");
        var reloadedCurrent = await _db.LedgerEntries.IgnoreQueryFilters().SingleAsync(e => e.ReferenceId == "tx_current");
        var reloadedOk = await _db.LedgerEntries.IgnoreQueryFilters().SingleAsync(e => e.ReferenceId == "tx_ok");

        Assert.That(reloadedB2b.ConsolidationStatus, Is.EqualTo(ConsolidationStatuses.NotRequired));
        Assert.That(reloadedNotReq.ConsolidationStatus, Is.EqualTo(ConsolidationStatuses.NotRequired));
        Assert.That(reloadedCurrent.ConsolidationStatus, Is.EqualTo(ConsolidationStatuses.Pending));
        Assert.That(reloadedOk.ConsolidationStatus, Is.EqualTo(ConsolidationStatuses.Consolidated));

        await _eventBus.Received(1).PublishAsync(Arg.Any<ConsolidatedInvoiceIssuedIntegrationEvent>());
    }

    [Test]
    public async Task OverThreshold_B2c_IsExcludedFromBatch()
    {
        var ts = PriorMonthUtcMid();
        var small = SeedSale("tx_small", LhdnValidationStatuses.B2cReceipt, ConsolidationStatuses.Pending, ts);
        var big = new LedgerEntry(_orgId, LedgerReferenceTypes.GatewayPayment, "tx_big", "sale", "B2C");
        big.AddLine(AccountTypes.AssetCash, 10000.01m, "MYR", 10000.01m, "MYR");
        big.AddLine(AccountTypes.RevenueGross, -10000.01m, "MYR", -10000.01m, "MYR");
        big.ValidateBalanced();
        big.AssignB2cReceipt("RCPT-big");
        typeof(LedgerEntry).GetProperty(nameof(LedgerEntry.Timestamp))!.SetValue(big, ts);
        _db.LedgerEntries.Add(big);
        await _db.SaveChangesAsync();

        _job = new B2cConsolidationJob(
            _sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<B2cConsolidationJob>.Instance,
            new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Lhdn:B2cIndividualThresholdMyr"] = "10000" })
                .Build());

        await _job.RunOnceAsync(CancellationToken.None);

        var reloadedSmall = await _db.LedgerEntries.IgnoreQueryFilters().SingleAsync(e => e.ReferenceId == "tx_small");
        var reloadedBig = await _db.LedgerEntries.IgnoreQueryFilters().SingleAsync(e => e.ReferenceId == "tx_big");
        Assert.That(reloadedSmall.ConsolidationStatus, Is.EqualTo(ConsolidationStatuses.Consolidated));
        Assert.That(reloadedBig.ConsolidationStatus, Is.EqualTo(ConsolidationStatuses.NotRequired));
        Assert.That(reloadedBig.LhdnValidationStatus, Is.EqualTo(LhdnValidationStatuses.NeedsBuyerTin));
        await _eventBus.Received(1).PublishAsync(Arg.Is<ConsolidatedInvoiceIssuedIntegrationEvent>(e =>
            e.TotalIncludingTax < 10000m));
        _ = small;
    }
}
