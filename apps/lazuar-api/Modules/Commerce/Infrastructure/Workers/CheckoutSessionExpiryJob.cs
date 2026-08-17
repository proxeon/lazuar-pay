using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
    /// </summary>
    public async Task ExpireSessionsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();

        var now = DateTime.UtcNow;
        var expired = await db.CheckoutSessions
            .IgnoreQueryFilters()
            .Where(s => s.Status == "OPEN" && s.ExpiresAt < now)
            .ToListAsync(ct);

        if (expired.Count == 0)
        {
            return;
        }

        var couponIds = expired
            .Where(s => s.CouponId.HasValue)
            .Select(s => s.CouponId!.Value)
            .Distinct()
            .ToList();

        var coupons = couponIds.Count == 0
            ? []
            : await db.Coupons
                .IgnoreQueryFilters()
                .Where(c => couponIds.Contains(c.Id))
                .ToListAsync(ct);

        var couponMap = coupons.ToDictionary(c => c.Id);

        var expiredCount = 0;
        foreach (var session in expired)
        {
            if (!session.TryExpire())
            {
                continue;
            }

            if (session.CouponId.HasValue && couponMap.TryGetValue(session.CouponId.Value, out var coupon))
            {
                coupon.ReleaseReservation();
            }

            expiredCount++;
        }

        try
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Expired {Count} checkout session(s).", expiredCount);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "A session completed while the expiry job was running; leftover OPEN rows retry next tick.");
        }
    }
}
