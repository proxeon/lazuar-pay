using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace Modules.One.Infrastructure.Services;

/// <summary>
/// Token bucket for public signup. 10 attempts / 10 minutes per key (IP or email+IP).
/// </summary>
public sealed class PublicRegisterRateLimiter
{
    public const int Limit = 10;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, TokenBucketRateLimiter> _limiters = new(StringComparer.Ordinal);

    public async Task<bool> TryAcquireAsync(string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return true;
        }

        var limiter = _limiters.GetOrAdd(key, _ => new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = Limit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
            ReplenishmentPeriod = Window,
            TokensPerPeriod = Limit,
            AutoReplenishment = true
        }));

        using var lease = await limiter.AcquireAsync(1, ct);
        return lease.IsAcquired;
    }
}
