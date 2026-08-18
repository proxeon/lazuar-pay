using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.Commerce.Domain.Aggregates;

namespace Modules.Commerce.Infrastructure.Workers;

/// <summary>
/// Expires OPEN checkout sessions past ExpiresAt and releases reserved coupon inventory.
/// </summary>
public class CheckoutSessionExpiryJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CheckoutSessionExpiryJob> _logger;

    public CheckoutSessionExpiryJob(
        IServiceScopeFactory scopeFactory,
        ILogger<CheckoutSessionExpiryJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Checkout Session Expiry Job started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpireSessionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while expiring checkout sessions.");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    /// <summary>
    /// Exposed for unit tests — same core as the background loop body.
    /// One session per transaction so two replicas cannot ReleaseReservation twice.
    /// </summary>
    public async Task ExpireSessionsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var expiredCount = 0;

        while (true)
        {
            CheckoutSession? session;
            if (db.Database.IsRelational())
            {
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                session = await ClaimExpiredSessionAsync(db, ct);
                if (session == null)
                {
                    await tx.CommitAsync(ct);
                    break;
                }

                if (await ExpireOneAsync(db, session, ct))
                {
                    expiredCount++;
                }

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            else
            {
                session = await db.CheckoutSessions
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(s => s.Status == "OPEN" && s.ExpiresAt < DateTime.UtcNow, ct);
                if (session == null)
                {
                    break;
                }

                if (await ExpireOneAsync(db, session, ct))
                {
                    expiredCount++;
                }

                try
                {
                    await db.SaveChangesAsync(ct);
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogWarning(ex, "A session completed while the expiry job was running; leftover OPEN rows retry next tick.");
                    break;
                }
            }
        }

        if (expiredCount > 0)
        {
            _logger.LogInformation("Expired {Count} checkout session(s).", expiredCount);
        }
    }

    internal static async Task<CheckoutSession?> ClaimExpiredSessionAsync(
        CommerceDbContext db,
        CancellationToken ct)
    {
        const string sql = """
            SELECT * FROM commerce."CheckoutSessions"
            WHERE "Status" = 'OPEN' AND "ExpiresAt" < NOW()
            ORDER BY "ExpiresAt"
            LIMIT 1
            FOR UPDATE SKIP LOCKED;
            """;

        return await db.CheckoutSessions
            .FromSqlRaw(sql)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(ct);
    }

    private static async Task<bool> ExpireOneAsync(
        CommerceDbContext db,
        CheckoutSession session,
        CancellationToken ct)
    {
        if (!session.TryExpire())
        {
            return false;
        }

        if (!session.CouponId.HasValue)
        {
            return true;
        }

        var coupon = await db.Coupons
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == session.CouponId.Value, ct);
        coupon?.ReleaseReservation();
        return true;
    }
}
