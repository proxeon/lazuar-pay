using System.Collections.Concurrent;

namespace Lazuar.Pay.PublicPay;

internal static class PublicPayLimiter
{
    static readonly ConcurrentDictionary<string, List<long>> Hits = new(StringComparer.Ordinal);

    public static bool TryAcquire(string key, int max, int windowSeconds)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
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
}
