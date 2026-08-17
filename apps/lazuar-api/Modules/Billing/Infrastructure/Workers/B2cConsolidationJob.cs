using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.Billing.Contracts.Events;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;

namespace Modules.Billing.Infrastructure.Workers;

public class B2cConsolidationJob : BackgroundService
{
    private static readonly TimeZoneInfo MalaysiaTimeZone = ResolveMalaysiaTimeZone();

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<B2cConsolidationJob> _logger;
    private readonly decimal _individualThresholdMyr;

    public B2cConsolidationJob(
        IServiceScopeFactory scopeFactory,
        ILogger<B2cConsolidationJob> logger,
        IConfiguration? configuration = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _individualThresholdMyr = configuration?.GetValue("Lhdn:B2cIndividualThresholdMyr", 10000m) ?? 10000m;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Catch-up immediately (startup + after each scheduled fire) so downtime past the 28th
                // does not skip closed months until the following month.
                await CatchUpClosedPeriodsAsync(stoppingToken);

                var timeUntilExecution = CalculateTimeToNextConsolidation();
                _logger.LogInformation(
                    "Next B2C Consolidation scheduled in {DelayHours:F2} hours.",
                    timeUntilExecution.TotalHours);

                await Task.Delay(timeUntilExecution, stoppingToken);

                await CatchUpClosedPeriodsAsync(stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing B2C Consolidation worker.");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    /// <summary>
    /// Delay until the next 28th 02:00 MYT. If already past this month's target, targets next month.
    /// Catch-up runs separately so a late start still consolidates closed months.
    /// </summary>
    private TimeSpan CalculateTimeToNextConsolidation()
    {
        var nowUtc = DateTime.UtcNow;
        var mytNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, MalaysiaTimeZone);

        var targetMyt = new DateTime(mytNow.Year, mytNow.Month, 28, 2, 0, 0, DateTimeKind.Unspecified);

        if (mytNow >= targetMyt)
        {
            targetMyt = targetMyt.AddMonths(1);
        }

        var targetUtc = TimeZoneInfo.ConvertTimeToUtc(targetMyt, MalaysiaTimeZone);
        return targetUtc - nowUtc;
    }

    /// <summary>Runs catch-up for all closed months with pending B2C (used by hosted loop and module tests).</summary>
    internal Task RunOnceAsync(CancellationToken ct = default) => CatchUpClosedPeriodsAsync(ct);

    /// <summary>
    /// Processes every fully closed MYT calendar month that still has pending B2C ledger rows
    /// (not only the prior month). Caps lookback at 24 months.
    /// </summary>
    private async Task CatchUpClosedPeriodsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        var nowMyt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, MalaysiaTimeZone);
        var currentMonthStartMyt = new DateTime(nowMyt.Year, nowMyt.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var lookbackStartMyt = currentMonthStartMyt.AddMonths(-24);
        var lookbackStartUtc = TimeZoneInfo.ConvertTimeToUtc(lookbackStartMyt, MalaysiaTimeZone);
        var currentMonthStartUtc = TimeZoneInfo.ConvertTimeToUtc(currentMonthStartMyt, MalaysiaTimeZone);

        // Distinct closed-month timestamps among pending B2C rows.
        var pendingTimestamps = await db.LedgerEntries
            .IgnoreQueryFilters()
            .Where(e => e.CustomerType == "B2C"
                && e.ReferenceType != LedgerReferenceTypes.GatewayRefund
                && e.Timestamp >= lookbackStartUtc
                && e.Timestamp < currentMonthStartUtc
                && (e.ConsolidationStatus == ConsolidationStatuses.Pending
                    || (e.ConsolidationStatus == null
                        && (e.LhdnValidationStatus == LhdnValidationStatuses.B2cReceipt
                            || e.LhdnValidationStatus == null))))
            .Select(e => e.Timestamp)
            .ToListAsync(ct);

        if (pendingTimestamps.Count == 0) return;

        var periodStarts = pendingTimestamps
            .Select(ts =>
            {
                var myt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(ts, DateTimeKind.Utc), MalaysiaTimeZone);
                return new DateTime(myt.Year, myt.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
            })
            .Distinct()
            .OrderBy(p => p)
            .ToList();

        var eventBus = scope.ServiceProvider.GetRequiredKeyedService<IEventBus>("BillingEventBus");

        foreach (var periodStartMyt in periodStarts)
        {
            var periodEndMyt = periodStartMyt.AddMonths(1);
            await ProcessPeriodAsync(db, eventBus, periodStartMyt, periodEndMyt, ct);
        }
    }

    /// <summary>Consolidates one MYT calendar month for all orgs with pending B2C rows.</summary>
    internal async Task ProcessPeriodAsync(
        BillingDbContext db,
        IEventBus eventBus,
        DateTime periodStartMyt,
        DateTime periodEndMyt,
        CancellationToken ct)
    {
        var periodStartUtc = TimeZoneInfo.ConvertTimeToUtc(periodStartMyt, MalaysiaTimeZone);
        var periodEndUtc = TimeZoneInfo.ConvertTimeToUtc(periodEndMyt, MalaysiaTimeZone);
        var periodKey = periodStartMyt.ToString("yyyyMM");

        var uninvoicedEntries = await db.LedgerEntries
            .IgnoreQueryFilters()
            .Include(e => e.Lines)
            .Where(e => e.CustomerType == "B2C"
                && e.ReferenceType != LedgerReferenceTypes.GatewayRefund
                && e.Timestamp >= periodStartUtc
                && e.Timestamp < periodEndUtc
                && (e.ConsolidationStatus == ConsolidationStatuses.Pending
                    || (e.ConsolidationStatus == null
                        && (e.LhdnValidationStatus == LhdnValidationStatuses.B2cReceipt
                            || e.LhdnValidationStatus == null))))
            .ToListAsync(ct);

        if (!uninvoicedEntries.Any()) return;

        var entriesByOrg = uninvoicedEntries.GroupBy(e => e.OrganizationId);

        foreach (var orgGroup in entriesByOrg)
        {
            try
            {
                await ProcessOrgPeriodAsync(db, eventBus, orgGroup.Key, orgGroup.ToList(), periodKey, ct);
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "B2C consolidation failed for Org {OrgId} period {Period}; continuing other orgs.",
                    orgGroup.Key, periodKey);

                // Drop failed org mutations so the next org is not poisoned by the tracker.
                foreach (var entry in orgGroup)
                {
                    var tracked = db.Entry(entry);
                    if (tracked.State != EntityState.Detached)
                    {
                        tracked.State = EntityState.Unchanged;
                        foreach (var line in entry.Lines)
                        {
                            var lineEntry = db.Entry(line);
                            if (lineEntry.State != EntityState.Detached)
                                lineEntry.State = EntityState.Unchanged;
                        }
                    }
                }
            }
        }
    }

    private async Task ProcessOrgPeriodAsync(
        BillingDbContext db,
        IEventBus eventBus,
        Guid orgId,
        List<LedgerEntry> entries,
        string periodKey,
        CancellationToken ct)
    {
        var consolidationRef = $"B2C-CONS-{periodKey}-{orgId:N}";
        var alreadyConsolidated = await db.LedgerEntries
            .IgnoreQueryFilters()
            .AnyAsync(e =>
                e.OrganizationId == orgId
                && e.TaxInvoiceId == consolidationRef, ct);

        if (alreadyConsolidated)
        {
            _logger.LogInformation(
                "Skipping B2C consolidation for Org {OrgId} period {Period} — already issued ({Ref}).",
                orgId, periodKey, consolidationRef);
            return;
        }

        var overThreshold = new List<LedgerEntry>();
        var batch = new List<LedgerEntry>();
        foreach (var entry in entries)
        {
            if (PaidAmount(entry) > _individualThresholdMyr)
            {
                entry.MarkConsolidationNotRequired();
                entry.UpdateLhdnStatus(null, LhdnValidationStatuses.NeedsBuyerTin);
                overThreshold.Add(entry);
            }
            else
            {
                batch.Add(entry);
            }
        }

        if (overThreshold.Count > 0)
        {
            _logger.LogInformation(
                "Excluded {Count} B2C rows over RM {Threshold} from {Ref}.",
                overThreshold.Count, _individualThresholdMyr, consolidationRef);
        }

        entries = batch;
        if (entries.Count == 0) return;

        var revenueLines = entries.SelectMany(e => e.Lines)
            .Where(l => l.AccountType == AccountTypes.RevenueGross
                        || l.AccountType == AccountTypes.LiabilityTaxPayable
                        || l.AccountType == AccountTypes.ContraRevenueRefunds)
            .ToList();

        if (!revenueLines.Any())
        {
            foreach (var entry in entries)
            {
                entry.MarkConsolidationIgnored();
            }
            return;
        }

        var groupedLines = revenueLines.GroupBy(l => new { l.TaxTypeCode, l.MsicCode });
        var items = new List<ConsolidatedLineItemDto>();
        decimal totalExcludingTax = 0;
        decimal totalTax = 0;

        foreach (var group in groupedLines)
        {
            var grossRevenue = -group
                .Where(l => l.AccountType == AccountTypes.RevenueGross)
                .Sum(l => l.BaseCurrencyAmount)
                - group
                .Where(l => l.AccountType == AccountTypes.ContraRevenueRefunds)
                .Sum(l => l.BaseCurrencyAmount);

            var taxAmount = -group
                .Where(l => l.AccountType == AccountTypes.LiabilityTaxPayable)
                .Sum(l => l.BaseCurrencyAmount);

            if (grossRevenue > 0)
            {
                if (taxAmount < 0) taxAmount = 0;

                items.Add(new ConsolidatedLineItemDto(
                    Description: "Consolidated B2C Sales",
                    ClassificationCode: group.Key.MsicCode,
                    Quantity: 1,
                    UnitPrice: grossRevenue,
                    TaxRate: taxAmount > 0 ? Math.Round((taxAmount / grossRevenue) * 100, 2) : 0,
                    TaxAmount: taxAmount,
                    Subtotal: grossRevenue,
                    TaxTypeCode: group.Key.TaxTypeCode
                ));

                totalExcludingTax += grossRevenue;
                totalTax += taxAmount;
            }
        }

        if (!items.Any() || totalExcludingTax <= 0)
        {
            foreach (var entry in entries)
            {
                entry.MarkConsolidationIgnored();
            }
            return;
        }

        var consolidationEvent = new ConsolidatedInvoiceIssuedIntegrationEvent(
            OrganizationId: orgId,
            InternalReferenceId: consolidationRef,
            IssueDate: DateTime.UtcNow,
            Items: items,
            TotalExcludingTax: totalExcludingTax,
            TotalTax: totalTax,
            TotalIncludingTax: totalExcludingTax + totalTax
        );

        await eventBus.PublishAsync(consolidationEvent);

        foreach (var entry in entries)
        {
            entry.MarkConsolidatedPending(consolidationRef);
        }

        _logger.LogInformation(
            "Consolidated {Count} B2C entries for Org {OrgId} period {Period}. Ref: {Ref}",
            entries.Count, orgId, periodKey, consolidationRef);
    }

    internal static decimal PaidAmount(LedgerEntry entry) =>
        entry.Lines
            .Where(l => l.AccountType == AccountTypes.AssetCash)
            .Sum(l => l.BaseCurrencyAmount);

    private static TimeZoneInfo ResolveMalaysiaTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
        }
    }
}
