using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.Billing.Contracts.Events;
using Modules.Billing.Domain;

namespace Modules.Billing.Infrastructure.Workers;

public class B2cConsolidationJob : BackgroundService
{
    private static readonly TimeZoneInfo MalaysiaTimeZone = ResolveMalaysiaTimeZone();

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<B2cConsolidationJob> _logger;

    public B2cConsolidationJob(IServiceScopeFactory scopeFactory, ILogger<B2cConsolidationJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var timeUntilExecution = CalculateTimeToNextConsolidation();

                _logger.LogInformation("Next B2C Consolidation scheduled in {DelayHours:F2} hours.", timeUntilExecution.TotalHours);

                await Task.Delay(timeUntilExecution, stoppingToken);

                await ProcessB2cConsolidationAsync(stoppingToken);
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

    /// <summary>Runs one consolidation pass (used by hosted loop and module tests).</summary>
    internal Task RunOnceAsync(CancellationToken ct = default) => ProcessB2cConsolidationAsync(ct);

    private async Task ProcessB2cConsolidationAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var eventBus = scope.ServiceProvider.GetRequiredKeyedService<IEventBus>("BillingEventBus");

        // Prior calendar month in Asia/Kuala_Lumpur.
        var nowMyt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, MalaysiaTimeZone);
        var periodStartMyt = new DateTime(nowMyt.Year, nowMyt.Month, 1, 0, 0, 0, DateTimeKind.Unspecified).AddMonths(-1);
        var periodEndMyt = periodStartMyt.AddMonths(1);
        var periodStartUtc = TimeZoneInfo.ConvertTimeToUtc(periodStartMyt, MalaysiaTimeZone);
        var periodEndUtc = TimeZoneInfo.ConvertTimeToUtc(periodEndMyt, MalaysiaTimeZone);
        var periodKey = periodStartMyt.ToString("yyyyMM");

        // Eligible: B2C receipts pending consolidation (plus legacy null status for backfill).
        // Exclude already consolidated / not required / ignored via ConsolidationStatus when present.
        // Worker has no ambient TenantId — fail-closed global filter would hide all rows.
        var uninvoicedEntries = await db.LedgerEntries
            .IgnoreQueryFilters()
            .Include(e => e.Lines)
            .Where(e => e.CustomerType == "B2C"
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
            var orgId = orgGroup.Key;
            var entries = orgGroup.ToList();

            // Per-org/month idempotency key — not "any B2C-CONS today".
            var consolidationRef = $"B2C-CONS-{periodKey}-{orgId:N}";
            var alreadyConsolidated = await db.LedgerEntries.AnyAsync(e =>
                e.OrganizationId == orgId
                && e.TaxInvoiceId == consolidationRef, ct);

            if (alreadyConsolidated)
            {
                _logger.LogInformation(
                    "Skipping B2C consolidation for Org {OrgId} period {Period} — already issued ({Ref}).",
                    orgId, periodKey, consolidationRef);
                continue;
            }

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
                continue;
            }

            var groupedLines = revenueLines.GroupBy(l => new { l.TaxTypeCode, l.MsicCode });
            var items = new System.Collections.Generic.List<ConsolidatedLineItemDto>();
            decimal totalExcludingTax = 0;
            decimal totalTax = 0;

            foreach (var group in groupedLines)
            {
                // Signed double-entry: revenue credits are negative; flip for display.
                // Refunds (contra revenue positive debit, or negative gross reversals) net correctly.
                var grossRevenue = -group
                    .Where(l => l.AccountType == AccountTypes.RevenueGross)
                    .Sum(l => l.BaseCurrencyAmount)
                    - group
                    .Where(l => l.AccountType == AccountTypes.ContraRevenueRefunds)
                    .Sum(l => l.BaseCurrencyAmount);

                var taxAmount = -group
                    .Where(l => l.AccountType == AccountTypes.LiabilityTaxPayable)
                    .Sum(l => l.BaseCurrencyAmount);

                // Only emit lines with net positive sales after refunds.
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
                continue;
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

        await db.SaveChangesAsync(ct);
    }

    private static TimeZoneInfo ResolveMalaysiaTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kuala_Lumpur");
        }
        catch (TimeZoneNotFoundException)
        {
            // Windows fallback
            return TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
        }
    }
}
