using System.Collections.Concurrent;

namespace Lazuar.Pay.PublicPay;

internal static class PublicPayLimiter
{
    // Issue 016 (issues/001): the key is the raw {token} route value from unauthenticated
    // callers, and TryAcquire runs before any token validation or DB lookup — requests for
    // nonexistent tokens used to allocate a permanent dictionary entry each (no removal path
    // at all), so distinct junk tokens grew the process heap until OOM. Keys are now capped
    // (bounded per entry) and idle entries are periodically swept.
    const int MaxKeyLength = 256;
    const int SweepEveryCalls = 4096;
    const long SweepHorizonSeconds = 3600; // comfortably above every window in use

    static readonly ConcurrentDictionary<string, List<long>> Hits = new(StringComparer.Ordinal);
    static long Calls;

    public static bool TryAcquire(string key, int max, int windowSeconds)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (key.Length > MaxKeyLength)
        {
            // Long junk tokens must not buy unbounded memory; truncation keeps them limited.
            key = key[..MaxKeyLength];
        }

        if (Interlocked.Increment(ref Calls) % SweepEveryCalls == 0)
        {
            Sweep(now - SweepHorizonSeconds);
        }

        var list = Hits.GetOrAdd(key, static _ => []);
        lock (list)
        {
            list.RemoveAll(t => t < now - windowSeconds);
            if (list.Count >= max)
            {
                return false;
            }

            list.Add(now);
            return true;
        }
    }

    /// <summary>
    /// Drop keys whose every recorded hit is older than the cutoff. The pair overload of
    /// TryRemove only removes when the value instance still matches, so a concurrent
    /// re-created list cannot be evicted. Internal for tests.
    /// </summary>
    internal static void Sweep(long cutoffUnix)
    {
        foreach (var pair in Hits)
        {
            lock (pair.Value)
            {
                if (pair.Value.All(t => t < cutoffUnix))
                {
                    Hits.TryRemove(pair);
                }
            }
        }
    }

    internal static int TrackedKeys => Hits.Count;
}
