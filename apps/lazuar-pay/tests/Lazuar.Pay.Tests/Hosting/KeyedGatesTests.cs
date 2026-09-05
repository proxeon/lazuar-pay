using System.Collections.Concurrent;
using Lazuar.Pay.Hosting;

namespace Lazuar.Pay.Tests;

/// <summary>
/// Stress for the per-key mutex. Issues 005 (issues/003): the old eviction protocol let a
/// waiter run on an orphaned entry while a fresh arrival minted a second semaphore for the
/// same key — two critical sections at once, undetectable by any functional test because
/// the database backstops hid the corruption. These tests detect overlap directly and keep
/// the 002/017 invariant (the map must retire entries) under the churn that triggers the
/// race.
/// </summary>
public class KeyedGatesTests
{
    [Test]
    public async Task Critical_sections_for_one_key_never_overlap_while_the_map_churns()
    {
        var gates = new KeyedGates();
        var busy = new ConcurrentDictionary<string, int>();
        var violations = new ConcurrentQueue<string>();
        var tasks = new List<Task>();

        for (var worker = 0; worker < 8; worker++)
        {
            var w = worker;
            tasks.Add(Task.Run(async () =>
            {
                for (var i = 0; i < 500; i++)
                {
                    // Rotate keys so entries are created, retired, and re-created under
                    // load — the exact window where the old reinsertion check raced.
                    var key = "k" + (i % 3);
                    await gates.RunAsync(key, async () =>
                    {
                        if (!busy.TryAdd(key, w))
                        {
                            violations.Enqueue($"worker {w} entered busy key {key}");
                            return 0;
                        }

                        await Task.Yield();

                        if (!busy.TryRemove(new KeyValuePair<string, int>(key, w)))
                        {
                            violations.Enqueue($"worker {w} lost its busy claim on {key}");
                        }

                        return 0;
                    }, CancellationToken.None);
                }
            }, CancellationToken.None));
        }

        await Task.WhenAll(tasks);
        Assert.That(violations, Is.Empty, string.Join("; ", violations.Take(5)));
        Assert.That(gates.Count, Is.EqualTo(0), "retired keys must not stay in the map (002/017)");
    }

    [Test]
    public async Task Hot_single_key_stays_serialized()
    {
        var gates = new KeyedGates();
        var inside = 0;
        var overlaps = 0;
        var tasks = new List<Task>();

        for (var worker = 0; worker < 8; worker++)
        {
            tasks.Add(Task.Run(async () =>
            {
                for (var i = 0; i < 1000; i++)
                {
                    await gates.RunAsync("hot", async () =>
                    {
                        if (Interlocked.Increment(ref inside) != 1)
                        {
                            Interlocked.Increment(ref overlaps);
                        }

                        await Task.Yield();
                        Interlocked.Decrement(ref inside);
                        return 0;
                    }, CancellationToken.None);
                }
            }, CancellationToken.None));
        }

        await Task.WhenAll(tasks);
        Assert.That(overlaps, Is.EqualTo(0), "no two critical sections may run for the same key");
        Assert.That(inside, Is.EqualTo(0));
        Assert.That(gates.Count, Is.EqualTo(0));
    }
}
