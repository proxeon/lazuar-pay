using System;

namespace Modules.Lhdn.Infrastructure.Observability;

/// <summary>
/// Lhdn product observability knobs. Bind from configuration section <see cref="SectionName"/> ("Lhdn").
/// Falls back to legacy <c>Observability:LhdnStuckThreshold</c> when <c>Lhdn:StuckThreshold</c> is unset.
/// </summary>
public sealed class LhdnObservabilityOptions
{
    public const string SectionName = "Lhdn";

    /// <summary>
    /// TaxDocuments in PENDING/SUBMITTED older than this are counted as stuck.
    /// Default: 1 hour.
    /// </summary>
    public TimeSpan StuckThreshold { get; set; } = TimeSpan.FromHours(1);
}
