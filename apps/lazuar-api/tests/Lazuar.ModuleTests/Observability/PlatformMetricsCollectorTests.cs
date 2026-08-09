using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application.Observability;
using BuildingBlocks.Infrastructure.Observability;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Observability;

[TestFixture]
public class PlatformMetricsCollectorTests
{
    [Test]
    public async Task CollectAsync_With_Zero_Schemas_Returns_Empty_Schemas_And_Zeros()
    {
        // Unreachable host → open fails → DatabaseReachable false; empty schema list still honored.
        var collector = new PlatformMetricsCollector(
            "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=1",
            Options.Create(new ObservabilityOptions()),
            Array.Empty<IOutboxSchemaRegistration>(),
            Array.Empty<IPlatformMetricsContributor>(),
            NullLogger<PlatformMetricsCollector>.Instance);

        var snapshot = await collector.CollectAsync(CancellationToken.None);

        Assert.That(snapshot.Schemas, Is.Empty);
        Assert.That(snapshot.OutboxPendingCount, Is.EqualTo(0));
        Assert.That(snapshot.LhdnStuckCount, Is.EqualTo(0));
        Assert.That(snapshot.DatabaseReachable, Is.False);
        Assert.That(snapshot.Error, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void ContributionBag_Maps_LhdnStuckCount_Key()
    {
        var bag = new PlatformMetricsContributionBag();
        bag.SetLong(PlatformMetricsCollector.LhdnStuckCountKey, 7);

        Assert.That(bag.TryGetLong(PlatformMetricsCollector.LhdnStuckCountKey, out var value), Is.True);
        Assert.That(value, Is.EqualTo(7));
        Assert.That(PlatformMetricsCollector.LhdnStuckCountKey, Is.EqualTo("lhdn.stuck_count"));
    }

    [Test]
    public async Task ContributeAsync_FailSoft_Loop_Continues_After_Throw()
    {
        // Mirrors aggregator fail-soft: one broken contributor must not block others.
        var throwing = new StubContributor("broken", _ => throw new InvalidOperationException("boom"));
        var ok = new StubContributor("ok", ctx =>
        {
            ctx.Bag.SetLong("ok.count", 1);
            return Task.CompletedTask;
        });

        var context = new PlatformMetricsCollectContext
        {
            Connection = new Npgsql.NpgsqlConnection(
                "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none"),
            CollectedAtUtc = DateTime.UtcNow
        };

        foreach (var contributor in new IPlatformMetricsContributor[] { throwing, ok })
        {
            try
            {
                await contributor.ContributeAsync(context, CancellationToken.None);
            }
            catch
            {
                // fail-soft
            }
        }

        Assert.That(context.Bag.TryGetLong("ok.count", out var n), Is.True);
        Assert.That(n, Is.EqualTo(1));
    }

    private sealed class StubContributor : IPlatformMetricsContributor
    {
        private readonly Func<PlatformMetricsCollectContext, Task> _contribute;

        public StubContributor(string name, Func<PlatformMetricsCollectContext, Task> contribute)
        {
            Name = name;
            _contribute = contribute;
        }

        public string Name { get; }

        public Task ContributeAsync(PlatformMetricsCollectContext context, CancellationToken cancellationToken = default)
            => _contribute(context);
    }
}
