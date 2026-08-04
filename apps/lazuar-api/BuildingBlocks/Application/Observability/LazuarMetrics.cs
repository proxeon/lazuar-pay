using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;

namespace BuildingBlocks.Application.Observability;

/// <summary>
/// Platform counters via <see cref="System.Diagnostics.Metrics"/>.
/// Safe for Application-layer handlers (no Infrastructure dependency).
/// Gauges (outbox lag, LHDN stuck) are registered by Infrastructure snapshot publisher.
/// </summary>
public static class LazuarMetrics
{
    public const string MeterName = "Lazuar.Hub";

    internal static readonly Meter Meter = new(MeterName, "1.0.0");

    private static long _deadLetters;
    private static long _webhookFailed;
    private static long _dunningCancels;

    private static readonly Counter<long> DeadLettersCounter =
        Meter.CreateCounter<long>("lazuar.outbox.dead_letters", description: "Messages moved to Dead status (outbox/inbox)");

    private static readonly Counter<long> WebhookFailedCounter =
        Meter.CreateCounter<long>("lazuar.webhook.failed", description: "Outbound customer webhook delivery failures or payment webhook process failures");

    private static readonly Counter<long> DunningCancelsCounter =
        Meter.CreateCounter<long>("lazuar.dunning.cancels", description: "Subscriptions canceled by the dunning engine (grace exhausted, FinalAction=CANCEL)");

    public static void RecordDeadLetter()
    {
        Interlocked.Increment(ref _deadLetters);
        DeadLettersCounter.Add(1);
    }

    public static void RecordWebhookFailed(string? source = null)
    {
        Interlocked.Increment(ref _webhookFailed);
        if (string.IsNullOrEmpty(source))
        {
            WebhookFailedCounter.Add(1);
        }
        else
        {
            WebhookFailedCounter.Add(1, new KeyValuePair<string, object?>("source", source));
        }
    }

    public static void RecordDunningCancel()
    {
        Interlocked.Increment(ref _dunningCancels);
        DunningCancelsCounter.Add(1);
    }

    public static long DeadLettersTotal => Interlocked.Read(ref _deadLetters);
    public static long WebhookFailedTotal => Interlocked.Read(ref _webhookFailed);
    public static long DunningCancelsTotal => Interlocked.Read(ref _dunningCancels);
}
