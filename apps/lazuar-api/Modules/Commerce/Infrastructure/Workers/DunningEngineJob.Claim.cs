using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Modules.Billing.Contracts;
using Modules.Commerce.Domain;
using Modules.Commerce.Domain.Aggregates;
using Modules.Commerce.Domain.Entities;

namespace Modules.Commerce.Infrastructure.Workers;

public partial class DunningEngineJob
{
    private enum ClaimMode
    {
        PreDunning,
        PastDue
    }

    private async Task ProcessClaimedBatchAsync(
        ClaimMode mode,
        List<DunningCampaign> campaigns,
        bool whatsAppEnabled,
        CancellationToken ct)
    {
        var failedIds = new HashSet<Guid>();
        var processedIds = new HashSet<Guid>();

        for (var i = 0; i < BatchSize; i++)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            var eventBus = scope.ServiceProvider.GetRequiredKeyedService<IEventBus>("CommerceEventBus");
            var billing = scope.ServiceProvider.GetService<IBillingQueryService>();

            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx = null;
            Subscription? sub;
            var excludeIds = new HashSet<Guid>(failedIds);
            excludeIds.UnionWith(processedIds);

            try
            {
                if (db.Database.IsRelational())
                {
                    tx = await db.Database.BeginTransactionAsync(ct);
                    sub = await ClaimSubscriptionAsync(db, mode, excludeIds, ct);
                    if (sub == null)
                    {
                        await tx.RollbackAsync(ct);
                        break;
                    }
                }
                else
                {
                    sub = await ClaimSubscriptionInMemoryAsync(db, mode, excludeIds, ct);
                    if (sub == null) break;
                }

                try
                {
                    if (mode == ClaimMode.PreDunning)
                    {
                        await ProcessPreDunningSubscriptionAsync(db, eventBus, campaigns, sub, whatsAppEnabled, ct, billing);
                    }
                    else
                    {
                        await ProcessPastDueSubscriptionAsync(db, eventBus, campaigns, sub, whatsAppEnabled, ct, billing);
                    }

                    await db.SaveChangesAsync(ct);
                    if (tx != null) await tx.CommitAsync(ct);
                    processedIds.Add(sub.Id);
                }
                catch (Exception ex)
                {
                    failedIds.Add(sub.Id);
                    _logger.LogError(ex, "Dunning failed for subscription {Id}; continuing batch.", sub.Id);
                    if (tx != null) await tx.RollbackAsync(ct);
                }
            }
            finally
            {
                if (tx != null) await tx.DisposeAsync();
            }
        }
    }

    private static async Task<Subscription?> ClaimSubscriptionAsync(
        CommerceDbContext db,
        ClaimMode mode,
        IReadOnlyCollection<Guid> excludeIds,
        CancellationToken ct)
    {
        var excludeClause = excludeIds.Count == 0
            ? ""
            : $""" AND s."Id" NOT IN ({string.Join(",", excludeIds.Select(id => $"'{id}'"))})""";

        string sql = mode switch
        {
            ClaimMode.PreDunning => $"""
                SELECT s.* FROM commerce."Subscriptions" s
                WHERE s."Status" = 'ACTIVE'
                  AND s."CancelAtPeriodEnd" IS NOT TRUE
                  AND (s."CollectionPausedUntil" IS NULL OR s."CollectionPausedUntil" <= NOW())
                  AND s."NextBillingDate" IS NOT NULL
                  AND s."NextBillingDate" > NOW()
                  AND s."NextBillingDate" <= NOW() + INTERVAL '14 days'
                  {excludeClause}
                ORDER BY s."NextBillingDate"
                LIMIT 1
                FOR UPDATE SKIP LOCKED;
                """,
            ClaimMode.PastDue => $"""
                SELECT s.* FROM commerce."Subscriptions" s
                WHERE s."Status" = 'PAST_DUE'
                  AND s."NextBillingDate" IS NOT NULL
                  AND (s."DunningPausedUntil" IS NULL OR s."DunningPausedUntil" <= NOW())
                  {excludeClause}
                ORDER BY s."NextBillingDate"
                LIMIT 1
                FOR UPDATE SKIP LOCKED;
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

        var sub = await db.Subscriptions
            .FromSqlRaw(sql)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(ct);

        if (sub == null) return null;

        // Load reminder logs for catch-up matching (FromSql composition may not Include).
        await db.Entry(sub).Collection(s => s.ReminderLogs).LoadAsync(ct);
        return sub;
    }

    private static async Task<Subscription?> ClaimSubscriptionInMemoryAsync(
        CommerceDbContext db,
        ClaimMode mode,
        IReadOnlyCollection<Guid> excludeIds,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        IQueryable<Subscription> query = db.Subscriptions
            .Include(s => s.ReminderLogs)
            .IgnoreQueryFilters()
            .Where(s => !excludeIds.Contains(s.Id));

        query = mode switch
        {
            ClaimMode.PreDunning => query.Where(s =>
                s.Status == "ACTIVE"
                && !s.CancelAtPeriodEnd
                && (s.CollectionPausedUntil == null || s.CollectionPausedUntil <= now)
                && s.NextBillingDate != null
                && s.NextBillingDate > now
                && s.NextBillingDate <= now.AddDays(14)),
            ClaimMode.PastDue => query.Where(s =>
                s.Status == "PAST_DUE"
                && s.NextBillingDate != null
                && (s.DunningPausedUntil == null || s.DunningPausedUntil <= now)),
            _ => query.Where(_ => false)
        };

        return await query.OrderBy(s => s.NextBillingDate).FirstOrDefaultAsync(ct);
    }
}
