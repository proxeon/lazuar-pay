using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Modules.Commerce.Infrastructure.Security;

/// <summary>Per-IP and per-email+IP bucket for public portal magic-link. 5 / 10 minutes.</summary>
public sealed class PortalMagicLinkRateLimiter
{
    public const int Limit = 5;
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

    public static string ClientKey(HttpContext ctx, string? email)
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(normalized) ? $"ip:{ip}" : $"email:{normalized}|ip:{ip}";
    }
}
