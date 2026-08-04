using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.Billing.Domain;
using Modules.Billing.Domain.Aggregates;

namespace Modules.Billing.Infrastructure.Workers;

public class RevenueRecognitionJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RevenueRecognitionJob> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromHours(1);

    public RevenueRecognitionJob(IServiceScopeFactory scopeFactory, ILogger<RevenueRecognitionJob> logger)
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
                await ProcessRecognitionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing Revenue Recognition worker.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    private async Task ProcessRecognitionsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        
        var pendingSchedules = await db.DeferredRevenueSchedules
            .Where(s => s.Status != "COMPLETED")
            .ToListAsync(ct);

        if (!pendingSchedules.Any()) return;

        var now = DateTime.UtcNow;
        bool requiresSave = false;

        foreach (var schedule in pendingSchedules)
        {
            var amountToRecognize = schedule.Recognize(now);
            if (amountToRecognize > 0)
            {
                var entry = new LedgerEntry(
                    schedule.OrganizationId,
                    "REVENUE_RECOGNITION",
                    $"{schedule.LedgerEntryId}_{now:yyyyMMddHH}",
                    $"Automated revenue recognition for deferred schedule {schedule.Id}");

                entry.AddLine(AccountTypes.LiabilityDeferredRevenue, amountToRecognize, schedule.Currency, amountToRecognize, schedule.Currency);
                entry.AddLine(AccountTypes.RevenueRecognized, -amountToRecognize, schedule.Currency, -amountToRecognize, schedule.Currency);
                
                entry.ValidateBalanced();
                db.LedgerEntries.Add(entry);
                requiresSave = true;
            }
        }

        if (requiresSave)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Processed revenue recognition for {Count} schedules.", pendingSchedules.Count);
        }
    }
}
