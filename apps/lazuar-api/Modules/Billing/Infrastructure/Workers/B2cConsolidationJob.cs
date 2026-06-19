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

namespace Modules.Billing.Infrastructure.Workers;

public class B2cConsolidationJob : BackgroundService
{
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
        var mytNow = nowUtc.AddHours(8);

        var targetMyt = new DateTime(mytNow.Year, mytNow.Month, 28, 2, 0, 0, DateTimeKind.Unspecified);

        if (mytNow >= targetMyt)
        {
            targetMyt = targetMyt.AddMonths(1);
        }

        var targetUtc = targetMyt.AddHours(-8);
        return targetUtc - nowUtc;
    }

    private async Task ProcessB2cConsolidationAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var eventBus = scope.ServiceProvider.GetRequiredKeyedService<IEventBus>("BillingEventBus");

        var today = DateTime.UtcNow.Date;
        bool alreadyRan = await db.LedgerEntries.AnyAsync(e => 
            e.TaxInvoiceId != null && 
            e.TaxInvoiceId.StartsWith("B2C-CONS-") && 
            e.Timestamp.Date == today, ct);

        if (alreadyRan) return;

        var uninvoicedEntries = await db.LedgerEntries
            .Include(e => e.Lines)
            .Where(e => e.CustomerType == "B2C" && e.LhdnValidationStatus == null)
            .ToListAsync(ct);

        if (!uninvoicedEntries.Any()) return;

        var entriesByOrg = uninvoicedEntries.GroupBy(e => e.OrganizationId);

        foreach (var orgGroup in entriesByOrg)
        {
            var orgId = orgGroup.Key;
            var entries = orgGroup.ToList();

            var revenueLines = entries.SelectMany(e => e.Lines)
                                      .Where(l => l.AccountType == "REVENUE_GROSS" || l.AccountType == "LIABILITY_TAX_PAYABLE")
                                      .ToList();

            if (!revenueLines.Any())
            {
                foreach (var entry in entries)
                {
                    entry.UpdateLhdnStatus(null, "IGNORED_NO_REVENUE");
                }
                continue;
            }

            var groupedLines = revenueLines.GroupBy(l => new { l.TaxTypeCode, l.MsicCode });
            var items = new System.Collections.Generic.List<ConsolidatedLineItemDto>();
            decimal totalExcludingTax = 0;
            decimal totalTax = 0;

            foreach (var group in groupedLines)
            {
                var grossRevenue = group.Where(l => l.AccountType == "REVENUE_GROSS").Sum(l => Math.Abs(l.BaseCurrencyAmount));
                var taxAmount = group.Where(l => l.AccountType == "LIABILITY_TAX_PAYABLE").Sum(l => Math.Abs(l.BaseCurrencyAmount));

                if (grossRevenue > 0)
                {
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

            var consolidationRef = $"B2C-CONS-{DateTime.UtcNow:yyyyMMddHHmmss}-{orgId.ToString()[..8]}";

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
                entry.UpdateLhdnStatus(consolidationRef, "CONSOLIDATED_PENDING");
            }

            _logger.LogInformation("Consolidated {Count} B2C entries for Org {OrgId}. Ref: {Ref}", entries.Count, orgId, consolidationRef);
        }

        await db.SaveChangesAsync(ct);
    }
}
