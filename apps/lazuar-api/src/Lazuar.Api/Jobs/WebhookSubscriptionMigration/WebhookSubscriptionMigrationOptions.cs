namespace Lazuar.Api.Jobs.WebhookSubscriptionMigration;

/// <summary>
/// One-shot LHDN webhook registry backfill configuration (R41).
/// Bind from section <see cref="SectionName"/>; env overrides
/// <c>WEBHOOK_SUBSCRIPTION_MIGRATION_ENABLED</c> / <c>WEBHOOK_SUBSCRIPTION_MIGRATION_DRY_RUN</c>.
/// </summary>
public sealed class WebhookSubscriptionMigrationOptions
{
    public const string SectionName = "WebhookSubscriptionMigration";

    /// <summary>When false (default), the hosted migrator is not registered.</summary>
    public bool Enabled { get; set; }

    /// <summary>When true (default), evaluate rows but do not insert into One.</summary>
    public bool DryRun { get; set; } = true;

    /// <summary>Page size when scanning <c>lhdn.WebhookSubscriptions</c>.</summary>
    public int BatchSize { get; set; } = 500;
}
