using System.Collections.Concurrent;

namespace Lazuar.Pay.Hosting;

/// <summary>
/// Per-key mutex that does not grow forever. After the last waiter leaves, the
/// entry is removed. If a waiter arrives during removal, the entry is put back
/// so two threads never hold different semaphores for the same key.
/// </summary>
internal sealed class KeyedGates
{
    sealed class Entry
    {
        public readonly SemaphoreSlim Lock = new(1, 1);
        public int Refs;
    }

    readonly ConcurrentDictionary<string, Entry> _map = new(StringComparer.Ordinal);

    public int Count => _map.Count;

    public async Task<T> RunAsync<T>(string key, Func<Task<T>> work, CancellationToken ct)
    {
        var entry = _map.GetOrAdd(key, static _ => new Entry());
        Interlocked.Increment(ref entry.Refs);
        await entry.Lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await work().ConfigureAwait(false);
        }
        finally
        {
            entry.Lock.Release();
            if (Interlocked.Decrement(ref entry.Refs) == 0
                && _map.TryRemove(key, out var gone)
                && ReferenceEquals(gone, entry)
                && Volatile.Read(ref entry.Refs) != 0)
            {
                _map.TryAdd(key, entry);
            }
        }
    }
}
