using System;

namespace BuildingBlocks.Infrastructure.Configuration;

/// <summary>
/// Poll / schedule intervals for domain background workers.
/// Bind from configuration section <see cref="SectionName"/> ("Workers").
/// Defaults match historical hard-coded values.
/// </summary>
public sealed class BackgroundWorkerOptions
{
    public const string SectionName = "Workers";

    /// <summary>Outbound customer webhook dispatcher poll interval.</summary>
    public TimeSpan OutboundWebhookInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Broadcast fan-out poll interval.</summary>
    public TimeSpan BroadcastFanoutInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>LHDN PENDING document submission poll interval.</summary>
    public TimeSpan LhdnSubmissionInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>LHDN SUBMITTED document status poll interval.</summary>
    public TimeSpan LhdnStatusPollingInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Commerce billing engine cycle interval.</summary>
    public TimeSpan BillingEngineInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Commerce dunning engine cycle interval.</summary>
    public TimeSpan DunningEngineInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>How long a claimed LHDN/webhook lease hides a row from other workers.</summary>
    public TimeSpan ClaimLeaseDuration { get; set; } = TimeSpan.FromMinutes(2);
}
