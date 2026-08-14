using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace Modules.One.Infrastructure.Services;

/// <summary>
/// In-memory fixed-window style limiter for provision endpoint (single-instance staging).
/// Keys: global secret identity + per (external_product, external_org_id).
/// </summary>
public sealed class IntegratorProvisionRateLimiter
{
    private readonly ConcurrentDictionary<string, TokenBucketRateLimiter> _limiters = new(StringComparer.Ordinal);

    public async Task<bool> TryAcquireAsync(string key, int limitPerMinute, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key) || limitPerMinute <= 0)
        {
            return true;
        }

        var limiter = _limiters.GetOrAdd(key, _ => new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = limitPerMinute,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            TokensPerPeriod = limitPerMinute,
            AutoReplenishment = true
        }));

        using var lease = await limiter.AcquireAsync(1, ct);
        return lease.IsAcquired;
    }
}
