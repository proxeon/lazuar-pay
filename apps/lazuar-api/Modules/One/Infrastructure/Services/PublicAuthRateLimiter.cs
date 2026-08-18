using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace Modules.One.Infrastructure.Services;

/// <summary>
/// Token bucket for unauthenticated auth attempts (login, forgot-password, resend-verification).
/// 5 attempts / 10 minutes per key (email+IP). Empty keys are denied.
/// </summary>
public sealed class PublicAuthRateLimiter
{
    public const int Limit = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, TokenBucketRateLimiter> _limiters = new(StringComparer.Ordinal);

    public async Task<bool> TryAcquireAsync(string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
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
