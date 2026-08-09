using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace BuildingBlocks.Application.Observability;

/// <summary>
/// Module-owned product/health metrics plugin. BuildingBlocks aggregates contributions
/// without knowing module business tables (e.g. Lhdn TaxDocuments).
/// </summary>
public interface IPlatformMetricsContributor
{
    /// <summary>Stable key for merge / diagnostics (e.g. "lhdn", "commerce").</summary>
    string Name { get; }

    Task ContributeAsync(
        PlatformMetricsCollectContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Shared collection context: open Default connection + contribution bag.
/// Contributors must not dispose <see cref="Connection"/>.
/// </summary>
public sealed class PlatformMetricsCollectContext
{
    public required DbConnection Connection { get; init; }

    public required DateTime CollectedAtUtc { get; init; }

    public PlatformMetricsContributionBag Bag { get; } = new();
}

/// <summary>Bag of product gauges/counters keyed by stable names (e.g. <c>lhdn.stuck_count</c>).</summary>
public sealed class PlatformMetricsContributionBag
{
    private readonly Dictionary<string, long> _longs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _doubles = new(StringComparer.Ordinal);

    public void SetLong(string key, long value) => _longs[key] = value;

    public void SetDouble(string key, double value) => _doubles[key] = value;

    public bool TryGetLong(string key, out long value) => _longs.TryGetValue(key, out value);

    public IReadOnlyDictionary<string, long> Longs => _longs;

    public IReadOnlyDictionary<string, double> Doubles => _doubles;
}
