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
            var billing = scope.ServiceProvider.GetRequiredService<IBillingQueryService>();
            var crm = scope.ServiceProvider.GetService<Modules.CRM.Contracts.ICrmQueryService>();

            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx = null;
            Subscription? sub;
            var excludeIds = new HashSet<Guid>(failedIds);
            excludeIds.UnionWith(processedIds);

            var preDunningWindowDays = ResolvePreDunningClaimWindowDays(campaigns);

            try
            {
                if (db.Database.IsRelational())
                {
                    tx = await db.Database.BeginTransactionAsync(ct);
                    sub = await ClaimSubscriptionAsync(db, mode, excludeIds, preDunningWindowDays, ct);
                    if (sub == null)
                    {
                        await tx.RollbackAsync(ct);
                        break;
                    }
                }
                else
                {
                    sub = await ClaimSubscriptionInMemoryAsync(db, mode, excludeIds, preDunningWindowDays, ct);
                    if (sub == null) break;
                }

                try
                {
                    if (mode == ClaimMode.PreDunning)
                    {
                        await ProcessPreDunningSubscriptionAsync(db, eventBus, campaigns, sub, whatsAppEnabled, ct, billing, crm);
                    }
                    else
                    {
                        await ProcessPastDueSubscriptionAsync(db, eventBus, campaigns, sub, whatsAppEnabled, ct, billing, crm);
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

    /// <summary>
    /// Pre-dunning claim horizon. A −21 step cannot fire if we only load seats due within 14 days (B03-C15).
    /// Floor 14 keeps the default seed (−3) unchanged; cap 90 avoids an unbounded FOR UPDATE scan.
    /// </summary>
    internal static int ResolvePreDunningClaimWindowDays(IEnumerable<DunningCampaign> campaigns)
    {
        var maxNegative = campaigns
            .SelectMany(c => c.Steps)
            .Where(s => s.DayOffset < 0)
            .Select(s => Math.Abs(s.DayOffset))
            .DefaultIfEmpty(14)
            .Max();
        return Math.Clamp(maxNegative, 14, 90);
    }

    private static async Task<Subscription?> ClaimSubscriptionAsync(
        CommerceDbContext db,
        ClaimMode mode,
        IReadOnlyCollection<Guid> excludeIds,
        int preDunningWindowDays,
        CancellationToken ct)
    {
        var window = Math.Clamp(preDunningWindowDays, 1, 90);
        var excludeClause = excludeIds.Count == 0
            ? ""
            : """ AND s."Id" <> ALL({0})""";
        // Window is {0} when there is no exclude list; {1} after ALL({0}).
        var windowPlaceholder = excludeIds.Count == 0 ? "{0}" : "{1}";

        string sql = mode switch
        {
            ClaimMode.PreDunning => $"""
                SELECT s.* FROM commerce."Subscriptions" s
                WHERE s."Status" = 'ACTIVE'
                  AND s."CancelAtPeriodEnd" IS NOT TRUE
                  AND (s."CollectionPausedUntil" IS NULL OR s."CollectionPausedUntil" <= NOW())
                  AND (s."DunningPausedUntil" IS NULL OR s."DunningPausedUntil" <= NOW())
                  AND s."NextBillingDate" IS NOT NULL
                  AND s."NextBillingDate" > NOW()
                  AND s."NextBillingDate" <= NOW() + ({windowPlaceholder} * INTERVAL '1 day')
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

        var query = mode == ClaimMode.PreDunning
            ? excludeIds.Count == 0
                ? db.Subscriptions.FromSqlRaw(sql, window)
                : db.Subscriptions.FromSqlRaw(sql, excludeIds.ToArray(), window)
            : excludeIds.Count == 0
                ? db.Subscriptions.FromSqlRaw(sql)
                : db.Subscriptions.FromSqlRaw(sql, excludeIds.ToArray());
        var sub = await query
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
        int preDunningWindowDays,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var window = Math.Clamp(preDunningWindowDays, 1, 90);
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
                && (s.DunningPausedUntil == null || s.DunningPausedUntil <= now)
                && s.NextBillingDate != null
                && s.NextBillingDate > now
                && s.NextBillingDate <= now.AddDays(window)),
            ClaimMode.PastDue => query.Where(s =>
                s.Status == "PAST_DUE"
                && s.NextBillingDate != null
                && (s.DunningPausedUntil == null || s.DunningPausedUntil <= now)),
            _ => query.Where(_ => false)
        };

        return await query.OrderBy(s => s.NextBillingDate).FirstOrDefaultAsync(ct);
    }
}
