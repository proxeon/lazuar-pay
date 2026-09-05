using System.Collections.Concurrent;

namespace Lazuar.Pay.Hosting;

/// <summary>
/// Per-key mutex that does not grow forever: after the last holder leaves, the entry is
/// removed, so idle keys never accumulate (002/017). Issue 005 (issues/003): eviction was
/// a lock-free refcount protocol whose decrement→0 raced a concurrent acquirer's
/// increment — the acquirer could validate an entry that the retiring holder then removed
/// (or vice versa), leaving one runner on an orphaned semaphore while a fresh GetOrAdd
/// minted a second one for the same key: two critical sections at once. Map mutation and
/// refcount transitions are now atomic with each other under a tiny instance lock; the
/// entry semaphore still serializes the (potentially long) critical sections themselves,
/// so the global lock only ever guards nanosecond bookkeeping.
/// </summary>
internal sealed class KeyedGates
{
    sealed class Entry
    {
        public readonly SemaphoreSlim Lock = new(1, 1);
        public int Refs;
    }

    readonly ConcurrentDictionary<string, Entry> _map = new(StringComparer.Ordinal);

    /// <summary>
    /// Guards map mutation and refcount transitions so an entry can never be observed
    /// mapped with zero references. Never held while waiting on an <see cref="Entry.Lock"/>
    /// — release-side code releases the entry semaphore before taking this.
    /// </summary>
    readonly object _bookkeeping = new();

    public int Count
    {
        get
        {
            lock (_bookkeeping)
            {
                return _map.Count;
            }
        }
    }

    public async Task<T> RunAsync<T>(string key, Func<Task<T>> work, CancellationToken ct)
    {
        Entry entry;
        lock (_bookkeeping)
        {
            entry = _map.GetOrAdd(key, static _ => new Entry());
            entry.Refs += 1;
        }

        await entry.Lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await work().ConfigureAwait(false);
        }
        finally
        {
            entry.Lock.Release();
            lock (_bookkeeping)
            {
                // Removal only when the last reference left, under the same lock the
                // acquirer used — an entry handed out by Acquire is therefore guaranteed
                // to stay mapped (with its semaphore unique for the key) until every
                // holder is done. A removal can never orphan a runner or split a key
                // across two semaphores.
                if (--entry.Refs == 0)
                {
                    _map.TryRemove(key, out _);
                }
            }
        }
    }
}
